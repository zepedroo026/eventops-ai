import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './components/AppLayout';
import ErrorBoundary from './components/ErrorBoundary';
import PrivateRoute from './components/PrivateRoute';
import { ToastProvider } from './components/Toast';
import AdminDashboard from './pages/AdminDashboard';
import Dashboard from './pages/Dashboard';
import EventoDetalhe from './pages/EventoDetalhe';
import FornecedorPortal from './pages/FornecedorPortal';
import Login from './pages/Login';
import Profile from './pages/Profile';
import Register from './pages/Register';
import StaffDashboard from './pages/StaffDashboard';

export default function App() {
  return (
    <ErrorBoundary>
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login"    element={<Login />} />
          <Route path="/register" element={<Register />} />

          <Route element={<PrivateRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard"        element={<Dashboard />} />
              <Route path="/staff-dashboard"  element={<StaffDashboard />} />
              <Route path="/admin"            element={<AdminDashboard />} />
              <Route path="/perfil"              element={<Profile />} />
              <Route path="/eventos/:id"        element={<EventoDetalhe />} />
              <Route path="/fornecedor-portal"  element={<FornecedorPortal />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </ToastProvider>
    </ErrorBoundary>
  );
}
