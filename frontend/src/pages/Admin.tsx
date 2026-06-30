import React, { useEffect, useState } from 'react';
import { Routes, Route, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../api/client';
import { Button } from '../components/Button';
import { cn } from '../lib/utils';

function AdminOverview() {
  const [stats, setStats] = useState<any>(null);
  useEffect(() => {
    api.getAdminStats().then(setStats).catch(console.error);
  }, []);

  if (!stats) return <div className="animate-pulse h-32 bg-m3-surface-container rounded-3xl"></div>;

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      <div className="bg-m3-surface-container rounded-3xl p-6">
        <h3 className="text-m3-on-surface-variant font-medium mb-2">Total Users</h3>
        <p className="text-4xl font-bold">{stats.totalUsers}</p>
      </div>
      <div className="bg-m3-surface-container rounded-3xl p-6">
        <h3 className="text-m3-on-surface-variant font-medium mb-2">Total Files</h3>
        <p className="text-4xl font-bold">{stats.totalFiles}</p>
      </div>
      <div className="bg-m3-surface-container rounded-3xl p-6">
        <h3 className="text-m3-on-surface-variant font-medium mb-2">Storage Used</h3>
        <p className="text-4xl font-bold">{(stats.totalStorage / (1024 * 1024)).toFixed(2)} MB</p>
      </div>
    </div>
  );
}

function AdminUsers() {
  const [users, setUsers] = useState<any[]>([]);
  useEffect(() => {
    api.getAdminUsers({}).then(res => setUsers(res.items)).catch(console.error);
  }, []);

  const handleSuspend = async (id: string, current: boolean) => {
    if (!window.confirm(`Are you sure you want to ${current ? 'un-suspend' : 'suspend'} this user?`)) return;
    try {
      await api.suspendUser(id, !current);
      setUsers(users.map(u => u.id === id ? { ...u, isBanned: !current } : u));
    } catch (e: any) { alert(e.message); }
  };

  return (
    <div className="bg-m3-surface-container rounded-3xl p-6 overflow-x-auto">
      <table className="w-full text-left border-collapse min-w-[600px]">
        <thead>
          <tr className="border-b border-m3-outline-variant/30 text-sm">
            <th className="py-3">User</th>
            <th className="py-3">Role</th>
            <th className="py-3">Status</th>
            <th className="py-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-m3-outline-variant/10">
          {users.map(u => (
            <tr key={u.id}>
              <td className="py-4 font-medium">{u.username}</td>
              <td className="py-4 text-sm">{u.role}</td>
              <td className="py-4">
                {u.isBanned ? (
                  <span className="bg-m3-error text-m3-on-error text-xs px-2 py-1 rounded-md">Suspended</span>
                ) : (
                  <span className="bg-m3-secondary-container text-m3-on-secondary-container text-xs px-2 py-1 rounded-md">Active</span>
                )}
              </td>
              <td className="py-4 text-right">
                <Button variant="text" size="sm" onClick={() => handleSuspend(u.id, u.isBanned)}>
                  {u.isBanned ? 'Unsuspend' : 'Suspend'}
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AdminFiles() {
  const [files, setFiles] = useState<any[]>([]);
  useEffect(() => {
    api.getAdminFiles({}).then(res => setFiles(res.items)).catch(console.error);
  }, []);

  const handleDelete = async (id: string) => {
    if (!window.confirm('Delete this file permanently?')) return;
    try {
      await api.deleteFile(id);
      setFiles(files.filter(f => f.id !== id));
    } catch (e: any) { alert(e.message); }
  };

  return (
    <div className="bg-m3-surface-container rounded-3xl p-6 overflow-x-auto">
      <table className="w-full text-left border-collapse min-w-[600px]">
        <thead>
          <tr className="border-b border-m3-outline-variant/30 text-sm">
            <th className="py-3">File</th>
            <th className="py-3">Uploader</th>
            <th className="py-3">NSFW</th>
            <th className="py-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-m3-outline-variant/10">
          {files.map(f => (
            <tr key={f.id}>
              <td className="py-4 font-medium truncate max-w-[200px]">{f.fileName}</td>
              <td className="py-4 text-sm">{f.uploaderUsername}</td>
              <td className="py-4">{f.isNsfw ? 'Yes' : 'No'}</td>
              <td className="py-4 text-right flex justify-end gap-2">
                <a href={`/f/${f.slug}`} target="_blank" rel="noreferrer" className="text-m3-primary text-sm hover:underline">View</a>
                <button onClick={() => handleDelete(f.id)} className="text-m3-error text-sm hover:underline">Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function Admin() {
  const { user, isLoading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isLoading && (!user || user.role !== 'Admin')) {
      navigate('/forbidden');
    }
  }, [user, isLoading, navigate]);

  if (isLoading || !user || user.role !== 'Admin') return null;

  // Absolute paths: Admin is mounted under a splat route (admin/*), so relative
  // tab links would resolve against the current URL and keep appending segments.
  const tabs = [
    { to: '/admin', label: 'Overview', end: true },
    { to: '/admin/users', label: 'Users' },
    { to: '/admin/files', label: 'Files' },
    { to: '/admin/reports', label: 'Reports' },
    { to: '/admin/audit', label: 'Audit Log' },
  ];

  return (
    <div className="space-y-8 animate-in fade-in duration-300">
      <div>
        <h1 className="text-3xl font-bold flex items-center gap-3">
          <span className="material-symbols-outlined text-4xl text-m3-primary">admin_panel_settings</span>
          Admin Dashboard
        </h1>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-2 border-b border-m3-outline-variant/30">
        {tabs.map(tab => (
          <NavLink
            key={tab.label}
            to={tab.to}
            end={tab.end}
            className={({ isActive }) => cn(
              "px-4 py-2 rounded-t-lg font-medium whitespace-nowrap transition-colors",
              isActive ? "text-m3-primary border-b-2 border-m3-primary" : "text-m3-on-surface-variant hover:text-m3-on-surface hover:bg-m3-surface-container"
            )}
          >
            {tab.label}
          </NavLink>
        ))}
      </div>

      <div>
        <Routes>
          <Route index element={<AdminOverview />} />
          <Route path="users" element={<AdminUsers />} />
          <Route path="files" element={<AdminFiles />} />
          <Route path="reports" element={<div className="bg-m3-surface-container rounded-3xl p-6 text-center text-m3-on-surface-variant">Reports module pending implementation.</div>} />
          <Route path="audit" element={<div className="bg-m3-surface-container rounded-3xl p-6 text-center text-m3-on-surface-variant">Audit log pending implementation.</div>} />
        </Routes>
      </div>
    </div>
  );
}
