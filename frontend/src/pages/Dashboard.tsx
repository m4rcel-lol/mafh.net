import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api, isMockMode } from '../api/client';
import { useAuth } from '../contexts/AuthContext';
import { formatBytes } from '../lib/utils';
import { FileMetadata } from '../api/types';
import { Button } from '../components/Button';

const GREETINGS = ['Hello', 'Hola', 'Bonjour', 'Ciao', 'Guten Tag', 'Namaste', 'Konnichiwa', 'Mambo', 'Aloha', 'Hej'];
let lastGreetingIndex = -1;

function getRandomGreeting() {
  let newIndex;
  do {
    newIndex = Math.floor(Math.random() * GREETINGS.length);
  } while (newIndex === lastGreetingIndex && GREETINGS.length > 1);
  lastGreetingIndex = newIndex;
  return GREETINGS[newIndex];
}

export default function Dashboard() {
  const { user, isLoading: authLoading } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [greeting] = useState(getRandomGreeting());
  const [recentFiles, setRecentFiles] = useState<FileMetadata[]>([]);
  const [loading, setLoading] = useState(true);

  const uploadedCount = searchParams.get('uploaded');

  useEffect(() => {
    if (!user && !authLoading) {
      navigate('/login');
      return;
    }

    if (user) {
      api.getUploads({ uploader: user.username }).then(res => {
        setRecentFiles(res.items.filter(f => f.uploaderId === user.id).slice(0, 5));
      }).catch(console.error).finally(() => setLoading(false));
    }
  }, [user, authLoading, navigate]);

  if (authLoading || loading) return <div className="animate-pulse h-64 bg-m3-surface-container rounded-3xl m-4"></div>;
  if (!user) return null;

  const quota = user.storageQuota || 0;
  const storagePct = quota > 0 ? Math.min((user.storageUsed / quota) * 100, 100) : 0;
  const avgViews = user.uploadCount > 0 ? Math.round(user.totalViews / user.uploadCount) : 0;
  const downloadRate = user.totalViews > 0 ? Math.round((user.totalDownloads / user.totalViews) * 100) : 0;

  return (
    <div className="space-y-8 animate-in fade-in duration-500">
      {uploadedCount && (
        <div className="bg-m3-primary-container text-m3-on-primary-container p-4 rounded-xl flex items-center gap-3">
          <span className="material-symbols-outlined">check_circle</span>
          <span className="font-medium">Successfully uploaded {uploadedCount} {uploadedCount === '1' ? 'file' : 'files'}!</span>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div className="col-span-1 lg:col-span-12 bg-m3-surface-container-high rounded-[32px] p-8 flex justify-between items-center relative overflow-hidden">
          <div className="z-10">
            {isMockMode && <span className="text-m3-primary text-sm font-medium tracking-wide">MOCK MODE ACTIVE</span>}
            <h2 className="text-4xl font-light mt-2 mb-4">{greeting}, {user.username}!</h2>
            <p className="text-m3-on-surface-variant max-w-md">Ready to share something new? Manage your uploads and keep an eye on how they're doing.</p>
            <div className="flex gap-4 mt-8">
              <button onClick={() => navigate('/upload')} className="bg-m3-primary text-m3-on-primary px-6 py-3 rounded-full font-bold flex items-center gap-2 shadow-md hover:shadow-lg transition-shadow">
                <span className="material-symbols-outlined">upload</span> Upload Files
              </button>
              <button onClick={() => navigate('/uploads')} className="border border-m3-outline text-m3-primary px-6 py-3 rounded-full font-bold hover:bg-m3-outline-variant transition-colors">
                Browse Public
              </button>
            </div>
          </div>
          <div className="text-[120px] opacity-10 absolute right-[-20px] bottom-[-40px] pointer-events-none select-none">
            ✨
          </div>
        </div>

        <div className="col-span-1 md:col-span-3 bg-m3-surface-container rounded-[24px] p-6 flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-m3-on-surface-variant text-sm uppercase tracking-tighter">Storage Used</span>
            <span className="material-symbols-outlined">inventory_2</span>
          </div>
          <div className="text-3xl font-medium">{formatBytes(user.storageUsed)}</div>
          <div className="w-full bg-m3-outline-variant h-2 rounded-full mt-2 overflow-hidden">
            <div className="bg-m3-primary h-full transition-all" style={{ width: `${storagePct}%` }}></div>
          </div>
          <span className="text-xs text-m3-on-surface-variant mt-1">{storagePct.toFixed(1)}% of {formatBytes(quota)}</span>
        </div>

        <div className="col-span-1 md:col-span-3 bg-m3-surface-container rounded-[24px] p-6 flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-m3-on-surface-variant text-sm uppercase tracking-tighter">Total Uploads</span>
            <span className="material-symbols-outlined">trending_up</span>
          </div>
          <div className="text-3xl font-medium">{user.uploadCount.toLocaleString()}</div>
          <span className="text-xs text-m3-on-surface-variant mt-auto">Across all visibilities</span>
        </div>

        <div className="col-span-1 md:col-span-3 bg-m3-surface-container rounded-[24px] p-6 flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-m3-on-surface-variant text-sm uppercase tracking-tighter">Total Views</span>
            <span className="material-symbols-outlined">visibility</span>
          </div>
          <div className="text-3xl font-medium">{user.totalViews.toLocaleString()}</div>
          <span className="text-xs text-m3-on-surface-variant mt-auto">Avg. {avgViews}/file</span>
        </div>

        <div className="col-span-1 md:col-span-3 bg-m3-surface-container rounded-[24px] p-6 flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-m3-on-surface-variant text-sm uppercase tracking-tighter">Downloads</span>
            <span className="material-symbols-outlined">save</span>
          </div>
          <div className="text-3xl font-medium">{user.totalDownloads.toLocaleString()}</div>
          <span className="text-xs text-m3-on-surface-variant mt-auto">{downloadRate}% download rate</span>
        </div>
      </div>

      <div className="bg-m3-surface border border-m3-outline-variant rounded-[32px] overflow-hidden">
        <div className="px-8 py-4 border-b border-m3-outline-variant flex justify-between items-center">
          <h3 className="text-lg font-medium">Recent Public Uploads</h3>
          <span className="text-sm text-m3-primary cursor-pointer hover:underline" onClick={() => navigate('/uploads')}>View all</span>
        </div>

        {recentFiles.length > 0 ? (
          <div className="w-full">
            <div className="grid grid-cols-12 px-8 py-3 bg-m3-surface-container text-m3-on-surface-variant text-xs font-bold uppercase">
              <div className="col-span-6 md:col-span-5">File Name</div>
              <div className="hidden md:block md:col-span-2 text-center">Views</div>
              <div className="col-span-3 md:col-span-2 text-center">Visibility</div>
              <div className="col-span-3 md:col-span-2 text-center">Size</div>
            </div>

            <div className="divide-y divide-m3-outline-variant">
              {recentFiles.map(file => (
                <div key={file.id} className="grid grid-cols-12 px-8 py-4 items-center hover:bg-m3-secondary-container/20 transition-colors cursor-pointer" onClick={() => navigate(`/f/${file.slug}`)}>
                  <div className="col-span-6 md:col-span-5 flex items-center gap-3">
                    <span className="text-2xl">{file.mimeType.startsWith('image/') ? '🖼️' : '📄'}</span>
                    <div className="flex flex-col min-w-0">
                      <span className="font-medium truncate max-w-[200px]" title={file.fileName}>{file.fileName}</span>
                      <span className="text-xs text-m3-on-surface-variant">{new Date(file.uploadDate).toLocaleDateString()}</span>
                    </div>
                  </div>
                  <div className="hidden md:block md:col-span-2 text-center text-sm text-m3-on-surface-variant">
                    {file.views.toLocaleString()}
                  </div>
                  <div className="col-span-3 md:col-span-2 text-center text-sm text-m3-on-surface-variant">
                    {file.visibility}
                  </div>
                  <div className="col-span-3 md:col-span-2 text-center text-sm">
                    {formatBytes(file.size)}
                  </div>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="text-center py-12">
            <span className="material-symbols-outlined text-5xl text-m3-on-surface-variant mb-4">inventory_2</span>
            <p className="text-m3-on-surface-variant mb-4">You haven't uploaded anything public yet.</p>
            <Button onClick={() => navigate('/upload')} variant="tonal">Start Uploading</Button>
          </div>
        )}
      </div>
    </div>
  );
}
