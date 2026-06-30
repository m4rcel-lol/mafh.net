import React, { useEffect, useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../api/client';
import { ProfileLink } from '../api/types';
import { Button } from '../components/Button';
import { Input } from '../components/Input';
import { VerifiedBadge } from '../components/VerifiedBadge';

const MAX_LINKS = 8;

export default function Settings() {
  const { user, refreshUser } = useAuth();
  const [displayName, setDisplayName] = useState('');
  const [bio, setBio] = useState('');
  const [links, setLinks] = useState<ProfileLink[]>([]);
  const [nsfwPref, setNsfwPref] = useState(false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [avatarLoading, setAvatarLoading] = useState(false);
  const avatarInputRef = React.useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (user) {
      setDisplayName(user.displayName ?? '');
      setBio(user.bio ?? '');
      setLinks(user.links ?? []);
      setNsfwPref(user.nsfwPreference);
    }
  }, [user?.id]);

  const updateLink = (i: number, field: keyof ProfileLink, value: string) =>
    setLinks(prev => prev.map((l, idx) => (idx === i ? { ...l, [field]: value } : l)));
  const addLink = () => setLinks(prev => (prev.length >= MAX_LINKS ? prev : [...prev, { label: '', url: '' }]));
  const removeLink = (i: number) => setLinks(prev => prev.filter((_, idx) => idx !== i));

  const handleSave = async () => {
    setLoading(true);
    setMessage('');
    try {
      await api.updateMe({
        displayName: displayName.trim(),
        bio: bio.trim(),
        links: links.map(l => ({ label: l.label.trim(), url: l.url.trim() })).filter(l => l.label && l.url),
        nsfwPreference: nsfwPref,
      });
      await refreshUser();
      setMessage('Settings updated successfully.');
    } catch (err: any) {
      setMessage(err.message || 'Failed to update settings.');
    } finally {
      setLoading(false);
    }
  };

  const handleAvatarSelected = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setAvatarLoading(true);
    setMessage('');
    try {
      await api.updateAvatar(file);
      await refreshUser();
      setMessage('Avatar updated successfully.');
    } catch (err: any) {
      setMessage(err.message || 'Failed to update avatar.');
    } finally {
      setAvatarLoading(false);
      if (avatarInputRef.current) avatarInputRef.current.value = '';
    }
  };

  if (!user) return null;

  const inputBase = 'w-full rounded-md border border-m3-outline bg-m3-surface-container px-4 py-3 text-m3-on-surface placeholder:text-m3-on-surface-variant/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary';

  return (
    <div className="max-w-2xl mx-auto space-y-8 animate-in fade-in duration-300">
      <h1 className="text-3xl font-bold">Settings</h1>

      {message && (
        <div className="bg-m3-surface-container-high p-4 rounded-xl text-sm font-medium">
          {message}
        </div>
      )}

      <div className="bg-m3-surface-container rounded-3xl p-6 md:p-8 space-y-8">
        <section>
          <h2 className="text-xl font-bold mb-4">Profile</h2>
          <div className="flex items-center gap-6 mb-6">
            <div className="relative w-24 h-24 rounded-full bg-m3-surface-container-highest flex items-center justify-center text-4xl overflow-hidden shrink-0">
              {user.avatarUrl ? (
                <img src={user.avatarUrl} alt="Avatar" className="w-full h-full object-cover" />
              ) : (
                <span className="font-bold text-m3-primary">{user.username.charAt(0).toUpperCase()}</span>
              )}
              {avatarLoading && (
                <div className="absolute inset-0 bg-black/55 flex items-center justify-center" aria-busy="true" aria-label="Uploading avatar">
                  <div className="animate-spin rounded-full h-8 w-8 border-2 border-white border-t-transparent"></div>
                </div>
              )}
            </div>
            <div className="space-y-2">
              <p className="font-medium flex items-center gap-1.5">@{user.username} {user.isVerified && <VerifiedBadge className="h-4 w-4" />}</p>
              <input ref={avatarInputRef} type="file" accept="image/png,image/jpeg,image/webp,image/avif" className="hidden" onChange={handleAvatarSelected} disabled={avatarLoading} />
              <Button variant="tonal" onClick={() => avatarInputRef.current?.click()} size="sm" disabled={avatarLoading}>
                {avatarLoading ? 'Uploading…' : 'Change Avatar'}
              </Button>
            </div>
          </div>

          <div className="space-y-4">
            <Input
              label="Display Name"
              value={displayName}
              onChange={e => setDisplayName(e.target.value)}
              maxLength={80}
              placeholder="Shown on your profile"
            />
            <div>
              <label className="text-xs font-medium text-m3-on-surface-variant ml-1 mb-1 block">Bio</label>
              <textarea
                value={bio}
                onChange={e => setBio(e.target.value)}
                rows={3}
                maxLength={500}
                placeholder="Tell people a little about yourself…"
                className={`${inputBase} resize-none`}
              />
              <p className="text-[11px] text-m3-on-surface-variant text-right mt-1">{bio.length}/500</p>
            </div>

            <div>
              <div className="flex items-center justify-between ml-1 mb-1">
                <label className="text-xs font-medium text-m3-on-surface-variant">Links</label>
                <span className="text-[11px] text-m3-on-surface-variant">{links.length}/{MAX_LINKS}</span>
              </div>
              <div className="space-y-2">
                {links.map((link, i) => (
                  <div key={i} className="flex gap-2 items-center">
                    <input
                      value={link.label}
                      onChange={e => updateLink(i, 'label', e.target.value)}
                      maxLength={40}
                      placeholder="Label"
                      className={`${inputBase} w-1/3`}
                    />
                    <input
                      value={link.url}
                      onChange={e => updateLink(i, 'url', e.target.value)}
                      maxLength={300}
                      placeholder="https://…"
                      className={`${inputBase} flex-1`}
                    />
                    <button
                      type="button"
                      onClick={() => removeLink(i)}
                      className="p-2 text-m3-on-surface-variant hover:text-m3-error rounded-full hover:bg-m3-error/10 transition-colors shrink-0"
                      aria-label="Remove link"
                    >
                      <span className="material-symbols-outlined">close</span>
                    </button>
                  </div>
                ))}
                {links.length < MAX_LINKS && (
                  <Button variant="text" size="sm" onClick={addLink} icon={<span className="material-symbols-outlined">add</span>}>
                    Add link
                  </Button>
                )}
              </div>
            </div>
          </div>
        </section>

        <hr className="border-m3-outline-variant/30" />

        <section>
           <h2 className="text-xl font-bold mb-4">Preferences</h2>
           <label className="flex items-center gap-4 cursor-pointer p-4 -ml-4 rounded-xl hover:bg-m3-surface-container-high transition-colors max-w-md">
             <div className="relative flex items-center justify-center shrink-0">
               <input
                 type="checkbox"
                 checked={nsfwPref}
                 onChange={e => setNsfwPref(e.target.checked)}
                 className="appearance-none w-5 h-5 border-2 border-m3-outline rounded checked:bg-m3-primary checked:border-m3-primary transition-colors cursor-pointer"
               />
               <span className="material-symbols-outlined absolute text-m3-on-primary text-[16px] pointer-events-none" style={{ opacity: nsfwPref ? 1 : 0 }}>check</span>
             </div>
             <div>
               <p className="font-medium text-sm">Show NSFW Content</p>
               <p className="text-xs text-m3-on-surface-variant">Allow sensitive content to appear in public feeds.</p>
             </div>
           </label>
        </section>

        <div className="flex justify-end pt-4">
           <Button onClick={handleSave} disabled={loading} icon={<span className="material-symbols-outlined">save</span>}>
             {loading ? 'Saving...' : 'Save Changes'}
           </Button>
        </div>
      </div>
    </div>
  );
}
