import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';

export default function Forbidden() {
  const navigate = useNavigate();
  return (
    <div className="text-center py-24 space-y-4 animate-in fade-in duration-300">
      <span className="material-symbols-outlined text-6xl text-m3-error">block</span>
      <h1 className="text-3xl font-bold">Access denied</h1>
      <p className="text-m3-on-surface-variant">You don't have permission to view this page.</p>
      <Button onClick={() => navigate('/')}>Back home</Button>
    </div>
  );
}
