import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import AppLayout from './components/AppLayout';
import PrivateRoute from './components/PrivateRoute';
import { ToastProvider } from './components/Toast';
import Dashboard from './pages/Dashboard';
import EventoDetalhe from './pages/EventoDetalhe';
import Login from './pages/Login';
import Register from './pages/Register';

export default function App() {
  return (
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login"    element={<Login />} />
          <Route path="/register" element={<Register />} />

          <Route element={<PrivateRoute />}>
            <Route element={<AppLayout />}>
              <Route path="/dashboard"   element={<Dashboard />} />
              <Route path="/eventos/:id" element={<EventoDetalhe />} />
            </Route>
          </Route>

          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </BrowserRouter>
    </ToastProvider>
  );
}
