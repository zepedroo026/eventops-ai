import { Navigate, Outlet } from 'react-router-dom';

export default function PrivateRoute({ children }) {
  if (!localStorage.getItem('token')) return <Navigate to="/login" replace />;
  return children ?? <Outlet />;
}
