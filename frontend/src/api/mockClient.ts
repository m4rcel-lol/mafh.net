import { ApiClient, FileMetadata, User } from './types';

const delay = (ms: number) => new Promise(res => setTimeout(res, ms));
const QUOTA = 10 * 1024 * 1024 * 1024;

let mockUsers: User[] = [
  { id: '1', username: 'admin', role: 'Admin', isBanned: false, storageUsed: 1024 * 1024 * 500, storageQuota: QUOTA, uploadCount: 42, totalViews: 1050, totalDownloads: 412, nsfwPreference: true },
  { id: '2', username: 'user1', role: 'User', isBanned: false, storageUsed: 1024 * 1024 * 50, storageQuota: QUOTA, uploadCount: 5, totalViews: 152, totalDownloads: 13, nsfwPreference: false },
  { id: '3', username: 'banned_user', role: 'User', isBanned: true, storageUsed: 0, storageQuota: QUOTA, uploadCount: 0, totalViews: 0, totalDownloads: 0, nsfwPreference: false },
];

let mockFiles: FileMetadata[] = [
  { id: 'f1', slug: 'demo-img', fileName: 'sunset.jpg', size: 1024 * 250, mimeType: 'image/jpeg', uploadDate: new Date().toISOString(), visibility: 'Public', tags: ['nature', 'sunset'], isNsfw: false, uploaderId: '1', uploaderUsername: 'admin', views: 150, downloads: 12 },
  { id: 'f2', slug: 'secret-doc', fileName: 'passwords.txt', size: 1024 * 5, mimeType: 'text/plain', uploadDate: new Date().toISOString(), visibility: 'Private', tags: ['private'], isNsfw: false, uploaderId: '2', uploaderUsername: 'user1', views: 2, downloads: 1 },
  { id: 'f3', slug: 'nsfw-meme', fileName: 'spicy.png', size: 1024 * 500, mimeType: 'image/png', uploadDate: new Date().toISOString(), visibility: 'Public', tags: ['meme'], isNsfw: true, uploaderId: '1', uploaderUsername: 'admin', views: 900, downloads: 400 },
];

let currentUser: User | null = null;

export const mockClient: ApiClient = {
  async register(data) {
    await delay(800);
    if (data.username === 'admin') throw new Error('Username taken');
    const newUser: User = { id: Math.random().toString(), username: data.username, role: 'User', isBanned: false, storageUsed: 0, storageQuota: QUOTA, uploadCount: 0, totalViews: 0, totalDownloads: 0, nsfwPreference: false };
    mockUsers.push(newUser);
    currentUser = newUser;
  },
  async login(data) {
    await delay(800);
    const user = mockUsers.find(u => u.username === data.username);
    if (!user) throw new Error('Invalid credentials');
    if (user.isBanned) throw new Error('Account Suspended');
    currentUser = user;
    return user;
  },
  async logout() {
    await delay(500);
    currentUser = null;
  },
  async getMe() {
    await delay(300);
    if (!currentUser) throw new Error('Unauthorized');
    return currentUser;
  },
  async forgotPassword() { await delay(800); },
  async resetPassword() { await delay(800); },

  async getUploads(params) {
    await delay(500);
    let items = mockFiles.filter(f => f.visibility === 'Public' && (currentUser?.nsfwPreference ? true : !f.isNsfw));
    if (params.uploader) items = items.filter(f => f.uploaderUsername === params.uploader);
    if (params.query) items = items.filter(f => f.fileName.includes(params.query) || f.tags.includes(params.query));
    return { items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1 };
  },
  async uploadFiles(files, visibility, tags, isNsfw, onProgress) {
    if (!currentUser) throw new Error('Unauthorized');
    for (let i = 0; i <= 100; i += 10) {
      await delay(100);
      onProgress?.(i);
    }
    const tagArray = tags ? tags.split(',').map(t => t.trim()) : [];
    const newFiles: FileMetadata[] = files.map(f => ({
      id: Math.random().toString(), slug: Math.random().toString(36).substring(7), fileName: f.name, size: f.size, mimeType: f.type || 'application/octet-stream', uploadDate: new Date().toISOString(), visibility: visibility as any, tags: tagArray, isNsfw, uploaderId: currentUser!.id, uploaderUsername: currentUser!.username, views: 0, downloads: 0
    }));
    mockFiles.push(...newFiles);
    return { successfulCount: newFiles.length, firstSlug: newFiles[0]?.slug, errors: [] };
  },
  async getFile(slug) {
    await delay(400);
    const file = mockFiles.find(f => f.slug === slug);
    if (!file) throw new Error('Not found');
    if (file.visibility === 'Private' && file.uploaderId !== currentUser?.id) throw new Error('Forbidden');
    return file;
  },
  async updateFile(id, data) {
    await delay(600);
    const file = mockFiles.find(f => f.id === id);
    if (!file) throw new Error('Not found');
    Object.assign(file, data);
    return file;
  },
  async deleteFile(id) {
    await delay(600);
    mockFiles = mockFiles.filter(f => f.id !== id);
  },
  async reportFile() { await delay(600); },

  async getUser(username) {
    await delay(400);
    const user = mockUsers.find(u => u.username === username);
    if (!user) throw new Error('Not found');
    return user;
  },
  async updateMe(data) {
    await delay(600);
    if (!currentUser) throw new Error('Unauthorized');
    Object.assign(currentUser, data);
    return currentUser;
  },
  async updatePassword() { await delay(600); },
  async updateAvatar() {
    await delay(800);
    if (!currentUser) throw new Error('Unauthorized');
    currentUser.avatarUrl = 'https://api.dicebear.com/7.x/avataaars/svg?seed=' + Math.random();
    return currentUser;
  },

  async getAdminStats() {
    await delay(400);
    return { totalUsers: mockUsers.length, totalFiles: mockFiles.length, totalStorage: mockFiles.reduce((acc, f) => acc + f.size, 0) };
  },
  async getAdminFiles() {
    await delay(500);
    return { items: mockFiles, totalCount: mockFiles.length, page: 1, pageSize: 20, totalPages: 1 };
  },
  async getAdminUsers() {
    await delay(500);
    return { items: mockUsers, totalCount: mockUsers.length, page: 1, pageSize: 20, totalPages: 1 };
  },
  async getAdminReports() {
    await delay(500);
    return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 1 };
  },
  async getAdminAuditLog() {
    await delay(500);
    return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 1 };
  },
  async updateUserRole(id, role) {
    await delay(500);
    const u = mockUsers.find(u => u.id === id);
    if (u) u.role = role as any;
  },
  async suspendUser(id, isBanned) {
    await delay(500);
    const u = mockUsers.find(u => u.id === id);
    if (u) u.isBanned = isBanned;
  },
  async resolveReport() { await delay(500); }
};
