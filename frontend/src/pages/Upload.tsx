import React, { useState, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { Button } from '../components/Button';
import { Input } from '../components/Input';
import { cn, formatBytes } from '../lib/utils';
import { useAuth } from '../contexts/AuthContext';

export default function Upload() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [files, setFiles] = useState<File[]>([]);
  const [visibility, setVisibility] = useState('Public');
  const [tags, setTags] = useState('');
  const [isNsfw, setIsNsfw] = useState(false);

  const [isDragging, setIsDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState('');

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      setFiles(prev => [...prev, ...Array.from(e.dataTransfer.files)]);
    }
  }, []);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFiles(prev => [...prev, ...Array.from(e.target.files!)]);
    }
  };

  const removeFile = (index: number) => {
    setFiles(prev => prev.filter((_, i) => i !== index));
  };

  const handleUpload = async () => {
    if (files.length === 0) return;
    setUploading(true);
    setError('');
    setProgress(0);

    try {
      const res = await api.uploadFiles(files, visibility, tags, isNsfw, (p) => setProgress(p));
      if (res.successfulCount === 1 && res.firstSlug) {
        navigate(`/f/${res.firstSlug}?uploaded=1`);
      } else {
        navigate(`/dashboard?uploaded=${res.successfulCount}`);
      }
    } catch (err: any) {
      setError(err.message || 'Upload failed');
      setUploading(false);
    }
  };

  if (!user) {
    return (
      <div className="text-center py-20">
        <h2 className="text-2xl font-bold mb-4">Login Required</h2>
        <Button onClick={() => navigate('/login')}>Login to Upload</Button>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
      <h1 className="text-3xl font-bold mb-8">Upload Files</h1>

      {error && (
        <div className="bg-m3-error-container text-m3-on-error-container p-4 rounded-xl mb-6 text-sm flex items-start gap-2">
          <span className="material-symbols-outlined text-lg">error</span>
          {error}
        </div>
      )}

      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => fileInputRef.current?.click()}
        className={cn(
          "border-2 border-dashed rounded-3xl p-12 text-center cursor-pointer transition-colors m3-ripple mb-8",
          isDragging ? "border-m3-primary bg-m3-primary/10" : "border-m3-outline-variant bg-m3-surface-container hover:bg-m3-surface-container-high"
        )}
      >
        <input
          type="file"
          multiple
          className="hidden"
          ref={fileInputRef}
          onChange={handleFileSelect}
          disabled={uploading}
        />
        <span className="material-symbols-outlined text-6xl text-m3-primary mb-4">cloud_upload</span>
        <h3 className="text-xl font-medium mb-2">Drag & Drop files here</h3>
        <p className="text-m3-on-surface-variant text-sm">or click to browse from your device</p>
      </div>

      {files.length > 0 && (
        <div className="bg-m3-surface-container rounded-3xl p-6 mb-8 space-y-6">
          <h3 className="text-lg font-medium">Selected Files ({files.length})</h3>
          <div className="space-y-2 max-h-60 overflow-y-auto pr-2">
            {files.map((f, i) => (
              <div key={i} className="flex items-center justify-between bg-m3-surface p-3 rounded-xl">
                <div className="flex items-center gap-3 overflow-hidden">
                  <span className="text-2xl flex-shrink-0">
                    {f.type.startsWith('image/') ? '🖼️' : '📄'}
                  </span>
                  <div className="min-w-0">
                    <p className="font-medium text-sm truncate">{f.name}</p>
                    <p className="text-xs text-m3-on-surface-variant">{formatBytes(f.size)}</p>
                  </div>
                </div>
                {!uploading && (
                  <button onClick={(e) => { e.stopPropagation(); removeFile(i); }} className="p-2 text-m3-on-surface-variant hover:text-m3-error transition-colors rounded-full hover:bg-m3-error/10">
                    <span className="material-symbols-outlined text-sm">close</span>
                  </button>
                )}
              </div>
            ))}
          </div>

          <div className="border-t border-m3-outline-variant/30 pt-6 space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
               <div>
                 <label className="text-xs font-medium text-m3-on-surface-variant ml-1 mb-1 block">Visibility</label>
                 <select
                   value={visibility}
                   onChange={e => setVisibility(e.target.value)}
                   disabled={uploading}
                   className="w-full h-14 rounded-md border border-m3-outline bg-m3-surface-container px-4 py-2 text-m3-on-surface focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary"
                 >
                   <option value="Public">Public</option>
                   <option value="Unlisted">Unlisted</option>
                   <option value="Private">Private</option>
                 </select>
               </div>
               <Input
                 label="Tags (comma separated)"
                 value={tags}
                 onChange={e => setTags(e.target.value)}
                 disabled={uploading}
                 placeholder="e.g. funny, nature"
               />
            </div>

            <label className="flex items-center gap-3 cursor-pointer w-fit p-2 rounded-xl hover:bg-m3-surface-container-high transition-colors">
              <div className="relative flex items-center justify-center">
                <input
                  type="checkbox"
                  checked={isNsfw}
                  onChange={e => setIsNsfw(e.target.checked)}
                  disabled={uploading}
                  className="appearance-none w-5 h-5 border-2 border-m3-outline rounded checked:bg-m3-primary checked:border-m3-primary transition-colors cursor-pointer"
                />
                <span className="material-symbols-outlined absolute text-m3-on-primary text-[16px] pointer-events-none" style={{ opacity: isNsfw ? 1 : 0 }}>check</span>
              </div>
              <span className="text-sm font-medium">Mark as NSFW</span>
            </label>
          </div>

          {uploading ? (
            <div className="space-y-2">
              <div className="flex justify-between text-sm">
                <span>Uploading...</span>
                <span>{Math.round(progress)}%</span>
              </div>
              <div className="w-full bg-m3-surface-container-highest rounded-full h-2 overflow-hidden">
                <div
                  className="bg-m3-primary h-full transition-all duration-200 ease-out"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>
          ) : (
            <div className="flex justify-end gap-3 pt-4">
              <Button variant="text" onClick={() => setFiles([])}>Clear All</Button>
              <Button onClick={handleUpload} icon={<span className="material-symbols-outlined">upload</span>}>
                Upload {files.length} {files.length === 1 ? 'File' : 'Files'}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
