/**
 * @license
 * SPDX-License-Identifier: Apache-2.0
 */

import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { Layout } from './components/Layout';

// Pages
import Home from './pages/Home';
import Uploads from './pages/Uploads';
import Upload from './pages/Upload';
import FilePreview from './pages/FilePreview';
import Profile from './pages/Profile';
import Dashboard from './pages/Dashboard';
import Settings from './pages/Settings';
import Login from './pages/Login';
import Register from './pages/Register';
import Admin from './pages/Admin';
import NotFound from './pages/NotFound';
import Forbidden from './pages/Forbidden';
import { ForgotPassword, ResetPassword } from './pages/AuthAux';
import { Terms, Privacy, Rules, Dmca, ErrorPage } from './pages/StaticPages';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route index element={<Home />} />
            <Route path="uploads" element={<Uploads />} />
            <Route path="upload" element={<Upload />} />
            <Route path="f/:slug" element={<FilePreview />} />
            <Route path="u/:username" element={<Profile />} />
            <Route path="dashboard" element={<Dashboard />} />
            <Route path="settings" element={<Settings />} />
            <Route path="login" element={<Login />} />
            <Route path="register" element={<Register />} />
            <Route path="forgot-password" element={<ForgotPassword />} />
            <Route path="reset-password" element={<ResetPassword />} />
            <Route path="admin/*" element={<Admin />} />
            <Route path="terms" element={<Terms />} />
            <Route path="privacy" element={<Privacy />} />
            <Route path="rules" element={<Rules />} />
            <Route path="dmca" element={<Dmca />} />
            <Route path="forbidden" element={<Forbidden />} />
            <Route path="error" element={<ErrorPage />} />
            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
