using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Groups.CreateGroup;

namespace Application.Groups.UploadGroupProfileImage;

public sealed record UploadGroupProfileImageCommand(
    Guid GroupId,
    MediaUploadRequest Upload) : ICommand<GroupResponse>;
