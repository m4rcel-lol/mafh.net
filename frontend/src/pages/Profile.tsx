import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { User, FileMetadata } from '../api/types';
import { formatBytes } from '../lib/utils';
import { Button } from '../components/Button';
import { VerifiedBadge } from '../components/VerifiedBadge';

export default function Profile() {
  const { username } = useParams<{ username: string }>();
  const navigate = useNavigate();
  const [profileUser, setProfileUser] = useState<User | null>(null);
  const [files, setFiles] = useState<FileMetadata[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!username) return;
    setLoading(true);
    Promise.all([
      api.getUser(username),
      api.getUploads({ uploader: username })
    ])
      .then(([u, f]) => {
        setProfileUser(u);
        setFiles(f.items.filter(file => file.uploaderUsername === username && file.visibility === 'Public'));
      })
      .catch(err => setError(err.message || 'User not found'))
      .finally(() => setLoading(false));
  }, [username]);

  if (loading) return <div className="animate-pulse h-64 bg-m3-surface-container rounded-3xl m-4"></div>;

  if (error || !profileUser) {
    return (
      <div className="text-center py-20 space-y-4">
        <span className="material-symbols-outlined text-6xl text-m3-error">person_off</span>
        <h2 className="text-2xl font-bold">{error || 'User not found'}</h2>
        <Button onClick={() => navigate('/')}>Go Home</Button>
      </div>
    );
  }

  if (profileUser.isBanned) {
     return (
      <div className="text-center py-20 space-y-4">
        <span className="material-symbols-outlined text-6xl text-m3-error">gavel</span>
        <h2 className="text-2xl font-bold">Account Suspended</h2>
        <p className="text-m3-on-surface-variant">This user account has been suspended.</p>
      </div>
    );
  }

  return (
    <div className="space-y-8 animate-in fade-in duration-500">
      <div className="bg-m3-surface-container rounded-3xl p-8 flex flex-col md:flex-row items-center md:items-start gap-8 text-center md:text-left">
        <div className="w-32 h-32 rounded-full bg-m3-surface-container-highest flex items-center justify-center text-5xl overflow-hidden shrink-0">
          {profileUser.avatarUrl ? (
            <img src={profileUser.avatarUrl} alt="Avatar" className="w-full h-full object-cover" />
          ) : (
            <span className="font-bold text-m3-primary">{(profileUser.displayName || profileUser.username).charAt(0).toUpperCase()}</span>
          )}
        </div>
        <div className="space-y-4 min-w-0">
          <div>
            <div className="flex items-center gap-2 justify-center md:justify-start">
              <h1 className="text-3xl font-bold break-words">{profileUser.displayName || `@${profileUser.username}`}</h1>
              {profileUser.isVerified && <VerifiedBadge className="h-6 w-6" />}
            </div>
            <div className="flex items-center gap-2 mt-1 text-m3-on-surface-variant justify-center md:justify-start flex-wrap">
              {profileUser.displayName && <span>@{profileUser.username}</span>}
              {profileUser.role === 'Admin' && (
                <span className="bg-m3-primary-container text-m3-on-primary-container text-[10px] font-bold px-2 py-0.5 rounded-sm uppercase tracking-wider">
                  Admin
                </span>
              )}
            </div>

            {profileUser.bio && (
              <p className="mt-3 text-m3-on-surface-variant max-w-prose whitespace-pre-line">{profileUser.bio}</p>
            )}

            {profileUser.links && profileUser.links.length > 0 && (
              <div className="flex flex-wrap gap-2 mt-3 justify-center md:justify-start">
                {profileUser.links.map((l, i) => (
                  <a
                    key={i}
                    href={l.url}
                    target="_blank"
                    rel="noreferrer noopener nofollow"
                    className="inline-flex items-center gap-1 bg-m3-surface-container-high px-3 py-1.5 rounded-full text-sm hover:bg-m3-surface-container-highest transition-colors max-w-full"
                  >
                    <span className="material-symbols-outlined text-[16px] shrink-0">link</span>
                    <span className="truncate">{l.label}</span>
                  </a>
                ))}
              </div>
            )}
          </div>
          <div className="flex gap-6 justify-center md:justify-start">
            <div>
              <p className="text-xl font-bold">{files.length}</p>
              <p className="text-xs text-m3-on-surface-variant uppercase tracking-wider font-medium">Public Files</p>
            </div>
            <div>
              <p className="text-xl font-bold">{formatBytes(files.reduce((a, b) => a + b.size, 0))}</p>
              <p className="text-xs text-m3-on-surface-variant uppercase tracking-wider font-medium">Shared</p>
            </div>
          </div>
        </div>
      </div>

      <div className="space-y-6">
        <h2 className="text-xl font-bold px-2">Public Uploads</h2>

        {files.length > 0 ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
            {files.map(file => (
              <div key={file.id} onClick={() => navigate(`/f/${file.slug}`)} className="bg-m3-surface-container rounded-2xl p-4 cursor-pointer hover:bg-m3-surface-container-high transition-colors m3-ripple relative">
                {file.isNsfw && (
                  <div className="absolute top-2 right-2 bg-m3-error text-m3-on-error text-[10px] font-bold px-2 py-1 rounded-full z-10 shadow-sm">
                    NSFW
                  </div>
                )}
                <div className="w-full h-32 bg-m3-surface-container-highest rounded-xl flex items-center justify-center mb-4 text-4xl overflow-hidden">
                  {file.mimeType.startsWith('image/') && !file.isNsfw ? (
                    <img src={`/api/uploads/${file.slug}/thumbnail`} alt="" loading="lazy" className="w-full h-full object-cover" onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }} />
                  ) : file.isNsfw ? (
                     <span className="text-6xl filter blur-sm">🔞</span>
                  ) : (
                    <span className="text-6xl">📄</span>
                  )}
                </div>
                <div className="space-y-1">
                  <p className="font-medium truncate">{file.fileName}</p>
                  <div className="flex justify-between text-xs text-m3-on-surface-variant">
                    <span>{formatBytes(file.size)}</span>
                    <span>{file.views} views</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="text-center py-20 bg-m3-surface-container rounded-3xl">
            <span className="material-symbols-outlined text-6xl text-m3-on-surface-variant mb-4">folder_open</span>
            <p className="text-m3-on-surface-variant">This user hasn't shared any public files yet.</p>
          </div>
        )}
      </div>
    </div>
  );
}
