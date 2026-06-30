import React, { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Button } from './Button';
import { cn } from '../lib/utils';
import { isMockMode } from '../api/client';

const UserMenu: React.FC<{ menuPosition: string }> = ({ menuPosition }) => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  if (!user) return null;

  const handleLogout = async () => {
    setOpen(false);
    await logout();
    navigate('/');
  };

  const go = (path: string) => { setOpen(false); navigate(path); };

  const Avatar = ({ className }: { className?: string }) => (
    user.avatarUrl
      ? <img src={user.avatarUrl} alt="Avatar" className={cn('w-full h-full object-cover', className)} />
      : <span className="font-bold text-m3-primary">{user.username.charAt(0).toUpperCase()}</span>
  );

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setOpen(o => !o)}
        className="w-10 h-10 rounded-full bg-m3-surface-container-high flex items-center justify-center overflow-hidden hover:bg-m3-surface-container-highest transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-m3-primary"
        title={`@${user.username}`}
        aria-haspopup="menu"
        aria-expanded={open}
      >
        <Avatar />
      </button>

      {open && (
        <div
          role="menu"
          className={cn(
            'absolute z-50 w-60 rounded-2xl bg-m3-surface-container-high border border-m3-outline-variant/30 shadow-xl p-2 animate-in fade-in slide-in-from-bottom-2 duration-150',
            menuPosition,
          )}
        >
          <div className="flex items-center gap-3 px-3 py-3">
            <div className="w-10 h-10 rounded-full bg-m3-surface-container-highest flex items-center justify-center overflow-hidden shrink-0">
              <Avatar />
            </div>
            <div className="min-w-0">
              <p className="text-xs text-m3-on-surface-variant">Signed in as</p>
              <p className="font-medium truncate">@{user.username}</p>
            </div>
          </div>

          <div className="h-px bg-m3-outline-variant/30 my-1" />

          <button onClick={() => go(`/u/${user.username}`)} role="menuitem" className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm hover:bg-m3-surface-container-highest transition-colors">
            <span className="material-symbols-outlined text-lg">account_circle</span> Profile
          </button>
          <button onClick={() => go('/settings')} role="menuitem" className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm hover:bg-m3-surface-container-highest transition-colors">
            <span className="material-symbols-outlined text-lg">settings</span> Settings
          </button>
          {user.role === 'Admin' && (
            <button onClick={() => go('/admin')} role="menuitem" className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm hover:bg-m3-surface-container-highest transition-colors">
              <span className="material-symbols-outlined text-lg">admin_panel_settings</span> Admin
            </button>
          )}

          <div className="h-px bg-m3-outline-variant/30 my-1" />

          <button onClick={handleLogout} role="menuitem" className="w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm text-m3-error hover:bg-m3-error/10 transition-colors">
            <span className="material-symbols-outlined text-lg">logout</span> Logout
          </button>
        </div>
      )}
    </div>
  );
};

export const Layout: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();

  const navItems = [
    { to: '/', icon: 'home', label: 'Home' },
    { to: '/uploads', icon: 'folder', label: 'Browse' },
    ...(user ? [
      { to: '/upload', icon: 'upload', label: 'Upload' },
      { to: '/dashboard', icon: 'dashboard', label: 'Dashboard' },
      { to: '/settings', icon: 'settings', label: 'Settings' }
    ] : []),
    ...(user?.role === 'Admin' ? [
      { to: '/admin', icon: 'admin_panel_settings', label: 'Admin' }
    ] : [])
  ];

  return (
    <div className="flex h-screen w-full bg-m3-surface text-m3-on-surface overflow-hidden">
      {/* Desktop Navigation Rail */}
      <nav className="hidden md:flex flex-col w-24 bg-m3-surface-container border-r border-m3-outline-variant py-6 items-center gap-8 z-20">
        <div className="w-12 h-12 bg-m3-primary rounded-2xl flex items-center justify-center text-m3-on-primary text-2xl shadow-lg">
          <span className="material-symbols-outlined">cloud</span>
        </div>

        {user && (
          <Button variant="fab" onClick={() => navigate('/upload')} className="mb-4 bg-m3-tertiary-container text-m3-on-tertiary-container shadow-xl hover:scale-110 active:scale-95 transition-all" title="Upload">
            <span className="material-symbols-outlined">add</span>
          </Button>
        )}

        <div className="flex flex-col gap-6 items-center w-full px-2 flex-1">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className="flex flex-col items-center gap-1 group cursor-pointer"
            >
              {({ isActive }) => (
                <>
                  <div className={cn(
                    "flex items-center justify-center w-14 h-8 rounded-full transition-colors",
                    isActive ? "bg-m3-secondary-container group-hover:bg-m3-on-secondary-container text-m3-on-secondary-container group-hover:text-m3-secondary-container" : "hover:bg-m3-secondary-container text-m3-on-surface-variant group-hover:text-m3-on-surface"
                  )}>
                    <span className={cn("material-symbols-outlined text-xl", isActive && "m3-filled-icon")}>
                      {item.icon}
                    </span>
                  </div>
                  <span className={cn("text-[11px] font-medium", isActive ? "text-m3-on-surface" : "text-m3-on-surface-variant")}>{item.label}</span>
                </>
              )}
            </NavLink>
          ))}
        </div>

        <div className="mt-auto px-2 w-full flex flex-col items-center gap-2">
          {!user ? (
            <Button variant="text" onClick={() => navigate('/login')} className="w-full">
              Login
            </Button>
          ) : (
            <UserMenu menuPosition="bottom-14 left-0" />
          )}
        </div>
      </nav>

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col min-w-0 overflow-y-auto overflow-x-hidden relative">
        {isMockMode && (
          <div className="bg-m3-error-container text-m3-on-error-container text-xs text-center py-1 font-bold z-50">
            DEVELOPMENT MOCK MODE
          </div>
        )}
        <div className="p-4 md:p-8 max-w-7xl mx-auto w-full pb-24 md:pb-8 flex-1">
          <Outlet />
        </div>
      </main>

      {/* Mobile Bottom Navigation */}
      <nav className="md:hidden fixed bottom-0 w-full h-20 bg-m3-surface-container border-t border-m3-outline-variant/30 flex justify-around items-center px-2 z-50 pb-safe">
        {navItems.slice(0, 4).map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) => cn(
              "flex flex-col items-center gap-1 p-2 rounded-full transition-colors flex-1 max-w-[80px]",
              isActive ? "text-m3-on-secondary-container" : "text-m3-on-surface-variant"
            )}
          >
            {({ isActive }) => (
              <>
                <div className={cn(
                  "flex items-center justify-center w-14 h-8 rounded-full transition-colors",
                  isActive && "bg-m3-secondary-container"
                )}>
                  <span className={cn("material-symbols-outlined", isActive && "m3-filled-icon")}>
                    {item.icon}
                  </span>
                </div>
                <span className="text-[11px] font-medium tracking-wide truncate w-full text-center">{item.label}</span>
              </>
            )}
          </NavLink>
        ))}
        {!user ? (
          <NavLink to="/login" className="flex flex-col items-center gap-1 p-2 rounded-full text-m3-on-surface-variant flex-1 max-w-[80px]">
            <div className="flex items-center justify-center w-14 h-8 rounded-full">
              <span className="material-symbols-outlined">login</span>
            </div>
            <span className="text-[11px] font-medium">Login</span>
          </NavLink>
        ) : (
          <div className="flex flex-col items-center gap-1 p-2 flex-1 max-w-[80px]">
            <UserMenu menuPosition="bottom-16 right-2" />
            <span className="text-[11px] font-medium text-m3-on-surface-variant">Account</span>
          </div>
        )}
      </nav>
    </div>
  );
};
