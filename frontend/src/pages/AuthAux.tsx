import React, { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import { Input } from '../components/Input';
import { Button } from '../components/Button';

export function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState('');

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.forgotPassword({ email });
      setMessage('If an account exists, a reset link has been sent.');
    } catch {
      setMessage('Failed to request reset.');
    }
  };

  return (
    <div className="max-w-md mx-auto pt-12 animate-in fade-in duration-300">
      <div className="bg-m3-surface-container rounded-[2rem] p-8">
        <h1 className="text-2xl font-bold mb-2">Forgot Password</h1>
        <p className="text-sm text-m3-on-surface-variant mb-6">Enter your email to receive a reset link.</p>
        {message && <div className="mb-4 text-sm text-m3-primary">{message}</div>}
        <form onSubmit={onSubmit} className="space-y-4">
          <Input label="Email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
          <Button type="submit" className="w-full">Send Link</Button>
        </form>
      </div>
    </div>
  );
}

export function ResetPassword() {
  const [searchParams] = useSearchParams();
  const [password, setPassword] = useState('');
  const [message, setMessage] = useState('');

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.resetPassword({
        userId: searchParams.get('userId'),
        token: searchParams.get('token'),
        password,
      });
      setMessage('Password reset successful. You can now login.');
    } catch {
      setMessage('Failed to reset password.');
    }
  };

  return (
    <div className="max-w-md mx-auto pt-12 animate-in fade-in duration-300">
      <div className="bg-m3-surface-container rounded-[2rem] p-8">
        <h1 className="text-2xl font-bold mb-6">Reset Password</h1>
        {message && <div className="mb-4 text-sm text-m3-primary">{message}</div>}
        <form onSubmit={onSubmit} className="space-y-4">
          <Input label="New Password" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
          <Button type="submit" className="w-full">Reset</Button>
        </form>
      </div>
    </div>
  );
}
