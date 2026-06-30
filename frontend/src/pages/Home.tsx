import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { api } from '../api/client';
import { FileMetadata } from '../api/types';
import { formatBytes } from '../lib/utils';
import { useAuth } from '../contexts/AuthContext';

export default function Home() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [recent, setRecent] = useState<FileMetadata[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getUploads({}).then(res => {
      setRecent(res.items.slice(0, 3));
      setLoading(false);
    }).catch(() => setLoading(false));
  }, []);

  return (
    <div className="flex flex-col gap-12 max-w-4xl mx-auto py-12 animate-in fade-in slide-in-from-bottom-4 duration-500 ease-out">
      <div className="text-center space-y-6">
        <h1 className="text-5xl md:text-6xl font-bold tracking-tight text-m3-on-surface">
          Share files securely.
        </h1>
        <p className="text-lg text-m3-on-surface-variant max-w-2xl mx-auto">
          A secure, fast, and reliable file sharing platform. Upload your files and share them instantly with anyone.
        </p>
        <div className="flex justify-center gap-4 pt-4">
          {user ? (
            <Button onClick={() => navigate('/upload')} size="lg" className="h-14 px-8 text-base shadow-lg shadow-m3-primary/20 hover:shadow-xl hover:shadow-m3-primary/30" icon={<span className="material-symbols-outlined text-2xl">upload</span>}>
              Upload Files
            </Button>
          ) : (
            <>
              <Button onClick={() => navigate('/register')} size="lg" className="h-14 px-8 text-base">Get Started</Button>
              <Button onClick={() => navigate('/login')} variant="tonal" size="lg" className="h-14 px-8 text-base">Login</Button>
            </>
          )}
        </div>
      </div>

      <div className="mt-12 space-y-6">
        <div className="flex items-center justify-between">
          <h2 className="text-2xl font-bold">Recent Uploads</h2>
          <Button variant="text" onClick={() => navigate('/uploads')}>View All</Button>
        </div>

        {loading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
            {[1, 2, 3].map(i => (
              <div key={i} className="h-48 bg-m3-surface-container rounded-2xl animate-pulse" />
            ))}
          </div>
        ) : recent.length > 0 ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
            {recent.map(file => (
              <div key={file.id} onClick={() => navigate(`/f/${file.slug}`)} className="bg-m3-surface-container rounded-2xl p-4 cursor-pointer hover:bg-m3-surface-container-high transition-colors m3-ripple">
                <div className="w-full h-32 bg-m3-surface-container-highest rounded-xl flex items-center justify-center mb-4 text-4xl">
                  {file.mimeType.startsWith('image/') ? '🖼️' : '📄'}
                </div>
                <div className="space-y-1">
                  <p className="font-medium truncate">{file.fileName}</p>
                  <div className="flex justify-between text-xs text-m3-on-surface-variant">
                    <span>{formatBytes(file.size)}</span>
                    <span>by {file.uploaderUsername}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-m3-on-surface-variant text-center py-8">No public files found.</p>
        )}
      </div>
    </div>
  );
}
