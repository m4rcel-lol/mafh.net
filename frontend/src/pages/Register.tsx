import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { Input } from '../components/Input';
import { Button } from '../components/Button';

export default function Register() {
  const navigate = useNavigate();
  const { register } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }
    setError('');
    setLoading(true);
    try {
      await register({ username, password });
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto w-full pt-12 animate-in slide-in-from-bottom-4 fade-in duration-500">
      <div className="bg-m3-surface-container rounded-[2rem] p-8 shadow-sm border border-m3-outline-variant/20">
        <div className="text-center mb-8">
          <span className="material-symbols-outlined text-5xl text-m3-primary mb-2">person_add</span>
          <h1 className="text-2xl font-bold">Create Account</h1>
          <p className="text-sm text-m3-on-surface-variant">Join to start sharing files</p>
        </div>

        {error && (
          <div className="bg-m3-error-container text-m3-on-error-container p-4 rounded-xl mb-6 text-sm flex items-start gap-2">
            <span className="material-symbols-outlined text-lg">error</span>
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <Input
            label="Username"
            value={username}
            onChange={e => setUsername(e.target.value)}
            required
            autoComplete="username"
          />
          <Input
            label="Password"
            type="password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
            autoComplete="new-password"
          />
          <Input
            label="Confirm Password"
            type="password"
            value={confirmPassword}
            onChange={e => setConfirmPassword(e.target.value)}
            required
            autoComplete="new-password"
          />

          <Button type="submit" className="w-full h-12 mt-4" disabled={loading}>
            {loading ? 'Creating...' : 'Create Account'}
          </Button>
        </form>

        <div className="mt-8 text-center text-sm text-m3-on-surface-variant">
          Already have an account?{' '}
          <Link to="/login" className="text-m3-primary font-medium hover:underline">
            Login
          </Link>
        </div>
      </div>
    </div>
  );
}
