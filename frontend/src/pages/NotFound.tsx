import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';

export default function NotFound() {
  const navigate = useNavigate();
  return (
    <div className="text-center py-24 space-y-4 animate-in fade-in duration-300">
      <div className="text-7xl">🪐</div>
      <h1 className="text-3xl font-bold">Lost in storage</h1>
      <p className="text-m3-on-surface-variant">That page does not exist.</p>
      <Button onClick={() => navigate('/')}>Back home</Button>
    </div>
  );
}
