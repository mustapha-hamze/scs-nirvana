using System.Collections.Generic;
using Application.Contracts.UserManagement;
using MediatR;

namespace Application.CQRS.Queries.UserManagement;

public record GetUserAttachmentsQuery(string UserId) : IRequest<List<UserAttachmentDto>>;
