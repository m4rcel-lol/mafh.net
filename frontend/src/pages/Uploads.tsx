import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { FileMetadata } from '../api/types';
import { formatBytes } from '../lib/utils';
import { Input } from '../components/Input';
import { Button } from '../components/Button';

export default function Uploads() {
  const navigate = useNavigate();
  const [files, setFiles] = useState<FileMetadata[]>([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState('');

  const fetchFiles = async (q: string = '') => {
    setLoading(true);
    try {
      const res = await api.getUploads({ query: q });
      setFiles(res.items);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchFiles();
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchFiles(query);
  };

  return (
    <div className="space-y-6 animate-in fade-in duration-300">
      <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Public Files</h1>
          <p className="text-m3-on-surface-variant">Browse globally shared files.</p>
        </div>
        <form onSubmit={handleSearch} className="flex gap-2 w-full md:w-auto">
          <Input
            placeholder="Search files or tags..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            className="w-full md:w-64"
          />
          <Button type="submit" variant="tonal" className="h-14"><span className="material-symbols-outlined">search</span></Button>
        </form>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
           {[...Array(8)].map((_, i) => (
             <div key={i} className="h-56 bg-m3-surface-container rounded-2xl animate-pulse" />
           ))}
        </div>
      ) : files.length > 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {files.map(file => (
            <div key={file.id} onClick={() => navigate(`/f/${file.slug}`)} className="bg-m3-surface-container rounded-2xl p-4 cursor-pointer hover:bg-m3-surface-container-high transition-colors m3-ripple relative group">
              {file.isNsfw && (
                <div className="absolute top-2 right-2 bg-m3-error text-m3-on-error text-[10px] font-bold px-2 py-1 rounded-full z-10 shadow-sm">
                  NSFW
                </div>
              )}
              <div className="w-full h-32 bg-m3-surface-container-highest rounded-xl flex items-center justify-center mb-4 text-4xl overflow-hidden relative">
                {file.mimeType.startsWith('image/') && !file.isNsfw ? (
                  <img src={`/api/uploads/${file.slug}/thumbnail`} alt="" loading="lazy" className="w-full h-full object-cover" onError={(e) => { (e.currentTarget as HTMLImageElement).replaceWith(Object.assign(document.createElement('span'), { className: 'text-6xl', textContent: '🖼️' })); }} />
                ) : file.isNsfw ? (
                   <span className="text-6xl filter blur-sm">🔞</span>
                ) : (
                  <span className="text-6xl">📄</span>
                )}
              </div>
              <div className="space-y-1">
                <p className="font-medium truncate" title={file.fileName}>{file.fileName}</p>
                <div className="flex justify-between text-xs text-m3-on-surface-variant">
                  <span>{formatBytes(file.size)}</span>
                  <span>{new Date(file.uploadDate).toLocaleDateString()}</span>
                </div>
                <div className="flex gap-1 overflow-x-hidden pt-1">
                  {file.tags.map(tag => (
                    <span key={tag} className="text-[10px] bg-m3-surface-container-highest px-2 py-0.5 rounded-md whitespace-nowrap">
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="text-center py-20 bg-m3-surface-container rounded-3xl">
          <span className="material-symbols-outlined text-6xl text-m3-on-surface-variant mb-4">search_off</span>
          <h3 className="text-xl font-medium">No files found</h3>
          <p className="text-m3-on-surface-variant">Try adjusting your search query.</p>
        </div>
      )}
    </div>
  );
}
