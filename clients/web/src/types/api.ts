// Shared enums and base types mirroring the backend DTOs. Enums are serialized
// as integers (ASP.NET default). Feature folders add their own request/response
// types and import these where needed.

export enum FriendsListVisibility {
  Public = 0,
  FriendsOnly = 1,
  Private = 2,
}

export enum GroupVisibility {
  Private = 0,
  Public = 1,
}

export enum GroupJoinPolicy {
  Open = 0,
  Requestable = 1,
  InvitationOnly = 2,
}

export enum GroupRole {
  Member = 0,
  Admin = 1,
  Owner = 2,
}

export enum BoardVisibility {
  AdminOnly = 0,
  MembersOnly = 1,
  ForAll = 2,
}

export enum BoardWriteability {
  AdminOnly = 0,
  MembersOnly = 1,
  ForAll = 2,
}

export enum ChatRoomKind {
  Direct = 0,
  Multi = 1,
  PlatformGroup = 2,
}

export enum ChatRoomParticipantRole {
  Owner = 0,
  Member = 1,
}

export enum MediaIntendedContext {
  Post = 0,
  Comment = 1,
  ChatMessage = 2,
}

export enum MediaKind {
  Video = 0,
  Image = 1,
}

export enum MediaProcessingStatus {
  PendingUpload = 0,
  Processing = 1,
  Ready = 2,
  Failed = 3,
}

export interface MediaAsset {
  id: number
  kind: MediaKind
  intendedContext: MediaIntendedContext
  processingStatus: MediaProcessingStatus
  mimeType: string
  originalFileName: string
  originalSizeBytes: number
  storedSizeBytes?: number
  failureReason?: string
  postId?: number
  commentId?: number
  chatMessageId?: number
  createdAt: string
  updatedAt: string
}
