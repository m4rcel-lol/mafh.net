export interface ProfileLink {
  label: string;
  url: string;
}

export interface User {
  id: string;
  username: string;
  displayName?: string;
  role: 'User' | 'Admin';
  isBanned: boolean;
  isVerified: boolean;
  avatarUrl?: string;
  bio?: string;
  links: ProfileLink[];
  storageUsed: number;
  storageQuota: number;
  uploadCount: number;
  totalViews: number;
  totalDownloads: number;
  nsfwPreference: boolean;
}

export interface FileMetadata {
  id: string;
  slug: string;
  fileName: string;
  size: number;
  mimeType: string;
  uploadDate: string;
  visibility: 'Public' | 'Unlisted' | 'Private';
  tags: string[];
  isNsfw: boolean;
  uploaderId: string;
  uploaderUsername: string;
  views: number;
  downloads: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ApiClient {
  // Auth
  register(data: any): Promise<void>;
  login(data: any): Promise<User>;
  logout(): Promise<void>;
  getMe(): Promise<User>;
  forgotPassword(data: any): Promise<void>;
  resetPassword(data: any): Promise<void>;

  // Uploads
  getUploads(params: any): Promise<PaginatedResponse<FileMetadata>>;
  uploadFiles(files: File[], visibility: string, tags: string, isNsfw: boolean, onProgress?: (progress: number) => void): Promise<{ successfulCount: number; firstSlug?: string; errors: string[] }>;
  getFile(slug: string): Promise<FileMetadata>;
  updateFile(id: string, data: any): Promise<FileMetadata>;
  deleteFile(id: string): Promise<void>;
  reportFile(id: string, reason: string): Promise<void>;

  // Users
  getUser(username: string): Promise<User>;
  updateMe(data: any): Promise<User>;
  updatePassword(data: any): Promise<void>;
  updateAvatar(file: File): Promise<User>;

  // Admin
  getAdminStats(): Promise<any>;
  getAdminFiles(params: any): Promise<PaginatedResponse<FileMetadata>>;
  getAdminUsers(params: any): Promise<PaginatedResponse<User>>;
  getAdminReports(params: any): Promise<PaginatedResponse<any>>;
  getAdminAuditLog(params: any): Promise<PaginatedResponse<any>>;
  updateUserRole(userId: string, role: string): Promise<void>;
  suspendUser(userId: string, isBanned: boolean): Promise<void>;
  verifyUser(userId: string, isVerified: boolean): Promise<void>;
  resolveReport(reportId: string, action: string): Promise<void>;
}
