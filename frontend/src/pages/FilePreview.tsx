import React, { useEffect, useState } from 'react';
import { useParams, useNavigate, useSearchParams, Link } from 'react-router-dom';
import { api } from '../api/client';
import { FileMetadata } from '../api/types';
import { formatBytes } from '../lib/utils';
import { Button } from '../components/Button';
import { useAuth } from '../contexts/AuthContext';

export default function FilePreview() {
  const { slug } = useParams<{ slug: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [file, setFile] = useState<FileMetadata | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revealed, setRevealed] = useState(false);
  const [mediaError, setMediaError] = useState(false);

  const isJustUploaded = searchParams.get('uploaded') === '1';

  useEffect(() => {
    if (!slug) return;
    setLoading(true);
    setMediaError(false);
    api.getFile(slug)
      .then(f => setFile(f))
      .catch(err => setError(err.message || 'File not found'))
      .finally(() => setLoading(false));
  }, [slug]);

  const handleDelete = async () => {
    if (!file || !window.confirm('Are you sure you want to delete this file? This cannot be undone.')) return;
    try {
      await api.deleteFile(file.id);
      navigate('/dashboard');
    } catch (err: any) {
      alert(err.message || 'Failed to delete');
    }
  };

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href.split('?')[0]);
      alert('Link copied to clipboard!');
    } catch {
      alert('Failed to copy link. Please copy it manually.');
    }
  };

  const handleReport = async () => {
    if (!file) return;
    const reason = window.prompt('Why are you reporting this file? (e.g. spam, illegal, abuse)');
    if (!reason || !reason.trim()) return;
    try {
      await api.reportFile(file.id, reason.trim());
      alert('Report submitted. Thank you.');
    } catch (err: any) {
      alert(err.message || 'Failed to submit report');
    }
  };

  if (loading) {
    return <div className="flex justify-center items-center h-64"><div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-m3-primary"></div></div>;
  }

  if (error || !file) {
    return (
      <div className="text-center py-20 space-y-4">
        <span className="material-symbols-outlined text-6xl text-m3-error">error</span>
        <h2 className="text-2xl font-bold">{error || 'File not found'}</h2>
        <Button onClick={() => navigate('/uploads')}>Browse Files</Button>
      </div>
    );
  }

  const isOwner = user?.id === file.uploaderId;
  const isAdmin = user?.role === 'Admin';
  const contentUrl = `/api/uploads/${file.slug}/content`;
  const mime = file.mimeType || '';
  const kind = mime.startsWith('image/') ? 'image'
    : mime.startsWith('video/') ? 'video'
    : mime.startsWith('audio/') ? 'audio'
    : mime === 'application/pdf' ? 'pdf'
    : 'other';

  const renderMedia = () => {
    if (file.isNsfw && !revealed) {
      return (
        <div className="text-center py-20 space-y-3">
          <span className="material-symbols-outlined text-6xl text-m3-on-surface-variant">visibility_off</span>
          <p className="text-m3-on-surface-variant">This content is marked NSFW.</p>
          <Button variant="tonal" onClick={() => setRevealed(true)}>Reveal content</Button>
        </div>
      );
    }
    if (mediaError) {
      return (
        <div className="text-center py-20 space-y-2">
          <span className="material-symbols-outlined text-6xl text-m3-on-surface-variant">hourglass_empty</span>
          <p className="text-m3-on-surface-variant text-sm">Preview unavailable — the file may still be processing.</p>
        </div>
      );
    }
    switch (kind) {
      case 'image':
        return <img src={contentUrl} alt={file.fileName} className="max-h-[70vh] w-auto object-contain" onError={() => setMediaError(true)} />;
      case 'video':
        return <video src={contentUrl} controls className="max-h-[70vh] w-full bg-black" onError={() => setMediaError(true)} />;
      case 'audio':
        return <div className="w-full px-6 py-16"><audio src={contentUrl} controls className="w-full" onError={() => setMediaError(true)} /></div>;
      case 'pdf':
        return <iframe src={contentUrl} title={file.fileName} className="w-full h-[70vh] bg-white" />;
      default:
        return (
          <div className="text-center py-20">
            <span className="text-8xl mb-4 block">📄</span>
            <p className="text-m3-on-surface-variant text-sm font-mono">No inline preview for this file type.</p>
          </div>
        );
    }
  };

  return (
    <div className="max-w-4xl mx-auto animate-in fade-in duration-500">
      {isJustUploaded && (
        <div className="bg-m3-primary-container text-m3-on-primary-container p-4 rounded-xl mb-6 flex items-center gap-3">
          <span className="material-symbols-outlined">check_circle</span>
          <span className="font-medium">File uploaded successfully!</span>
        </div>
      )}

      <div className="bg-m3-surface-container rounded-3xl overflow-hidden shadow-sm relative">
        {file.isNsfw && (
          <div className="absolute top-4 right-4 bg-m3-error text-m3-on-error text-xs font-bold px-3 py-1 rounded-full z-10 shadow-sm">
            NSFW Content
          </div>
        )}

        <div className="w-full min-h-[16rem] bg-m3-surface-container-highest flex items-center justify-center relative">
          {renderMedia()}
        </div>

        <div className="p-6 md:p-8">
          <div className="flex flex-col md:flex-row md:items-start justify-between gap-6 mb-8">
            <div>
              <h1 className="text-2xl md:text-3xl font-bold break-all mb-2">{file.fileName}</h1>
              <div className="flex flex-wrap gap-4 text-sm text-m3-on-surface-variant">
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-[16px]">calendar_today</span> {new Date(file.uploadDate).toLocaleDateString()}</span>
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-[16px]">hard_drive</span> {formatBytes(file.size)}</span>
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-[16px]">visibility</span> {file.views} views</span>
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-[16px]">download</span> {file.downloads} downloads</span>
              </div>
            </div>

            <div className="flex flex-wrap gap-3 shrink-0">
              <a href={`${contentUrl}?download=true`}>
                <Button icon={<span className="material-symbols-outlined">download</span>}>Download</Button>
              </a>
              <Button variant="tonal" onClick={handleCopyLink} icon={<span className="material-symbols-outlined">link</span>}>Share</Button>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 py-6 border-t border-m3-outline-variant/30">
            <div className="space-y-4">
              <h3 className="text-sm font-medium text-m3-on-surface-variant uppercase tracking-wider">Details</h3>
              <dl className="space-y-2 text-sm">
                <div className="flex justify-between"><dt className="text-m3-on-surface-variant">Type</dt><dd className="font-mono">{file.mimeType}</dd></div>
                <div className="flex justify-between"><dt className="text-m3-on-surface-variant">Visibility</dt><dd>{file.visibility}</dd></div>
                <div className="flex justify-between items-center">
                  <dt className="text-m3-on-surface-variant">Uploader</dt>
                  <dd><Link to={`/u/${file.uploaderUsername}`} className="hover:underline text-m3-primary font-medium">{file.uploaderUsername}</Link></dd>
                </div>
              </dl>
            </div>

            <div className="space-y-4">
              <h3 className="text-sm font-medium text-m3-on-surface-variant uppercase tracking-wider">Tags</h3>
              {file.tags && file.tags.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {file.tags.map(tag => (
                    <span key={tag} className="bg-m3-surface-container-high px-3 py-1 rounded-lg text-sm">
                      {tag}
                    </span>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-m3-on-surface-variant">No tags.</p>
              )}
            </div>
          </div>

          <div className="pt-6 border-t border-m3-outline-variant/30 flex justify-between items-center">
            <Button variant="text" onClick={handleReport} className="text-m3-error hover:bg-m3-error/10" icon={<span className="material-symbols-outlined">flag</span>}>
              Report
            </Button>

            {(isOwner || isAdmin) && (
              <Button variant="outlined" onClick={handleDelete} className="text-m3-error border-m3-error hover:bg-m3-error/10" icon={<span className="material-symbols-outlined">delete</span>}>
                Delete
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
