using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Application.UseCases.Utilities.ApplicationFunctions
{
    public class CodeGenerator
    {
        private readonly TimeProvider _timeProvider;

        public CodeGenerator(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public string GenerateAppKey(int applicationInternalId)
        {
            // One captured instant (not six separate DateTime.UtcNow reads), UTC, formatted
            // with invariant culture so it can't vary by host locale.
            var timestamp = _timeProvider.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

            return $"APP-KEY-NIRVANA-CMS-{timestamp}-{applicationInternalId}-{RandomNumberGenerator.GetInt32(int.MaxValue)}";
        }
    }
}
