#!/usr/bin/env python3
"""Static structure checks for the content delivery hardening scripts (no SQL Server needed).

Proves only that the scripts are internally consistent and match the adapter's mappings. It does
NOT prove Row-Level Security works; only 04/05 run on a restored backup can (README section 5).

Run from the repository root:  python3 docs/content-delivery-db-hardening/check_scripts.py
"""
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
DBCONTEXT = ROOT / "src/Core/ContentDelivery.SqlServer/ContentDeliveryDbContext.cs"
SCRIPTS = sorted(HERE.glob("*.sql"))
failures = []


def check(ok, message):
    if not ok:
        failures.append(message)


def read(name):
    return (HERE / name).read_text(encoding="utf-8")


def code_only(sql, strip_strings=False):
    """SQL without -- comments (and optionally without string literals)."""
    sql = re.sub(r"--[^\n]*", "", sql)
    return re.sub(r"N?'(?:[^']|'')*'", "''", sql) if strip_strings else sql


# Adapter mapping: table -> mapped columns, straight from ContentDeliveryDbContext.
cs = DBCONTEXT.read_text(encoding="utf-8")
row_tables = dict(re.findall(r'Entity<(\w+)>\(\)\.ToTable\("(\w+)"\)', cs))
mapped = {}
for cls, body in re.findall(r"class (\w+)\s*\{(.*?)\n\}", cs, re.S):
    if cls in row_tables:
        mapped[row_tables[cls]] = set(re.findall(r"public [\w?]+ (\w+) \{ get; set; \}", body))
check(len(mapped) == 11, f"expected 11 adapter tables, parsed {len(mapped)}")

deploy, preflight, rollback = read("03-deploy.sql"), read("01-preflight.sql"), read("06-rollback.sql")
verify, direct = read("04-verify-restored-backup.sql"), read("05-verify-website-connection.sql")

# Every script: fail fast, and every $(Var) it uses is named in its usage header.
for path in SCRIPTS:
    text = path.read_text(encoding="utf-8")
    check(":on error exit" in text, f"{path.name}: missing ':on error exit'")
    header = text.split(":on error exit")[0]
    for var in set(re.findall(r"\$\((\w+)\)", text)):
        check(var in header, f"{path.name}: SQLCMD variable {var} not documented in the usage header")
    check(":setvar" not in text, f"{path.name}: ':setvar' defaults would override -v values")

# Column-level grants == adapter mapping; policy covers exactly the adapter tables.
grants = {t: {c.strip().strip("[]") for c in cols.split(",")}
          for t, cols in re.findall(r"GRANT SELECT ON dbo\.(\w+) \((.*?)\) TO", deploy, re.S)}
check(grants == mapped, "deploy column grants differ from ContentDeliveryDbContext: "
      + str(sorted(t for t in set(grants) | set(mapped) if grants.get(t) != mapped.get(t))))
policy_tables = set(re.findall(r"ADD FILTER PREDICATE [\w.]+\([\w, ]+\) ON dbo\.(\w+)", deploy))
check(policy_tables == set(mapped), f"policy tables differ from adapter tables: {sorted(policy_tables ^ set(mapped))}")
pre = {}
for t, c in re.findall(r"\(N'(\w+)', N'(\w+)'\)", preflight.split("-- 1)")[0]):
    pre.setdefault(t, set()).add(c)
check(pre == mapped, "preflight @Delivery list differs from ContentDeliveryDbContext")

# Predicates: schema-bound, identity-based only, never caller-controlled state.
functions = re.findall(r"CREATE FUNCTION (ContentDeliverySecurity\.\w+)\((.*?)'\);", deploy, re.S)
check(len(functions) == 7, f"expected 7 predicate functions, found {len(functions)}")
for name, body in functions:
    check("WITH SCHEMABINDING" in body, f"{name}: not schema-bound")
    for banned in ("SESSION_CONTEXT", "CONTEXT_INFO", "APP_NAME", "HOST_NAME", "ORIGINAL_LOGIN", "SUSER_"):
        check(banned not in body.upper(), f"{name}: uses caller-controlled or login-level value {banned}")
    check(not re.search(r"(?<![\w.])(CMS_|GNR_)", body), f"{name}: table reference without schema prefix")
check("SCHEMABINDING = ON" in deploy, "policy must be created WITH SCHEMABINDING = ON")

# Least privilege: no broad roles, schema-wide or database-wide SELECT, or direct mapping access.
for banned in ("db_owner", "db_datareader", "GRANT SELECT ON SCHEMA", "GRANT CONTROL", "IMPERSONATE",
               "GRANT SELECT ON ContentDeliverySecurity", "GRANT EXECUTE"):
    check(banned.lower() not in code_only(deploy).lower(), f"deploy contains forbidden grant/role: {banned}")
check("DENY SELECT, INSERT, UPDATE, DELETE" in deploy and "ON SCHEMA::ContentDeliverySecurity" in deploy,
      "deploy must deny the website role on the security schema")
check("DENY INSERT, UPDATE, DELETE" in deploy, "deploy must deny website writes")

# Transactions: deploy/rollback atomic; restored-backup verification never commits.
for name, text in (("03-deploy.sql", deploy), ("06-rollback.sql", rollback)):
    check("SET XACT_ABORT ON" in text and "BEGIN TRANSACTION" in text and "COMMIT TRANSACTION" in text,
          f"{name}: must run in one XACT_ABORT transaction")
check("COMMIT" not in verify.upper() and "ROLLBACK TRANSACTION" in verify, "04 must always roll back")
check("RESTORED_COPY" in verify, "04 must require ConfirmRestoredCopy")

# Preflight is read-only (table-variable inserts aside).
code = code_only(preflight, strip_strings=True)
for kw in re.findall(r"\b(CREATE|ALTER|DROP|GRANT|DENY|REVOKE|UPDATE|DELETE|TRUNCATE|MERGE|EXEC|EXECUTE)\b", code, re.I):
    check(False, f"01-preflight.sql is not read-only: contains {kw}")
check(not re.search(r"INSERT INTO (?!@)", code), "01-preflight.sql inserts into a real table")

# Rollback removes every object deploy creates, in dependency-safe order.
created = {
    "FUNCTION": re.findall(r"CREATE FUNCTION (ContentDeliverySecurity\.\w+)", deploy),
    "TABLE": re.findall(r"CREATE TABLE (ContentDeliverySecurity\.\w+)", deploy),
    "SECURITY POLICY": re.findall(r"CREATE SECURITY POLICY (ContentDeliverySecurity\.\w+)", deploy),
    "ROLE": re.findall(r"CREATE ROLE (\w+)", deploy),
    "SCHEMA": re.findall(r"CREATE SCHEMA (\w+)", deploy),
}
for kind, names in created.items():
    for name in names:
        check(f"DROP {kind} {name}" in rollback, f"rollback does not drop {kind} {name}")
rb = code_only(rollback)
order = [rb.find("DROP USER"), rb.find("DROP ROLE"), rb.find("DROP SECURITY POLICY"),
         rb.find("DROP FUNCTION"), rb.find("DROP TABLE"), rb.find("DROP SCHEMA")]
check(all(i >= 0 for i in order) and order == sorted(order),
      "rollback order must be users, role, policy, functions, table, schema")
check(not re.search(r"\b(DELETE|TRUNCATE|UPDATE)\b|DROP TABLE dbo\.|ALTER TABLE", rb, re.I),
      "rollback must not touch CMS data or tables")

# No embedded credentials or connection strings anywhere in this folder.
for path in list(SCRIPTS) + [HERE / "README.md"]:
    text = path.read_text(encoding="utf-8")
    check(not re.search(r"(Password|Pwd)\s*=\s*(?!N?[\"']?[$<(])\S", text, re.I) or path.name == "02-create-website-login.sql",
          f"{path.name}: looks like an embedded password")
    check(not re.search(r"(Server|Data Source)\s*=\s*[^<\s]", text, re.I), f"{path.name}: looks like a connection string")
login = read("02-create-website-login.sql")
check(re.findall(r"PASSWORD = N''(.*?)''", login) == ["$(WebsiteLoginPassword)"],
      "02: the only password must be the WebsiteLoginPassword variable")

if failures:
    print("FAIL")
    for f in failures:
        print(" -", f)
    sys.exit(1)
print(f"OK: {len(SCRIPTS)} scripts, {len(mapped)} adapter tables, {len(functions)} predicate functions checked")
