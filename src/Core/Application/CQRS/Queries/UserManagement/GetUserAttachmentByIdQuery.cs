using Application.Contracts.UserManagement;
using MediatR;

namespace Application.CQRS.Queries.UserManagement;

public record GetUserAttachmentByIdQuery(int Id) : IRequest<UserAttachmentDto>;
