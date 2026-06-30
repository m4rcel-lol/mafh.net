import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../api/client';
import { Button } from '../components/Button';

export default function Settings() {
  const { user, refreshUser } = useAuth();
  const [nsfwPref, setNsfwPref] = useState(user?.nsfwPreference ?? false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [avatarLoading, setAvatarLoading] = useState(false);
  const avatarInputRef = React.useRef<HTMLInputElement>(null);

  const handleSave = async () => {
    setLoading(true);
    setMessage('');
    try {
      await api.updateMe({ nsfwPreference: nsfwPref });
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
          <div className="flex items-center gap-6">
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
              <p className="font-medium">@{user.username}</p>
              <input ref={avatarInputRef} type="file" accept="image/png,image/jpeg,image/webp,image/avif" className="hidden" onChange={handleAvatarSelected} disabled={avatarLoading} />
              <Button variant="tonal" onClick={() => avatarInputRef.current?.click()} size="sm" disabled={avatarLoading}>
                {avatarLoading ? 'Uploading…' : 'Change Avatar'}
              </Button>
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
