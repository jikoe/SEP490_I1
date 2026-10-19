import { Navigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { LoginPage } from '../../features/authentication/LoginPage';

function DashboardPage() {
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-slate-100 p-6">
      <div className="mx-auto max-w-4xl rounded-2xl bg-white p-8 shadow-soft">
        <div className="flex items-center justify-between gap-4 border-b border-slate-200 pb-6">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.2em] text-emerald-600">AgriSustain</p>
            <h1 className="mt-2 text-3xl font-bold text-slate-900">Welcome back</h1>
          </div>
          <button
            type="button"
            onClick={() => logout()}
            className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100"
          >
            Logout
          </button>
        </div>

        <div className="mt-8 grid gap-5 md:grid-cols-2">
          <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
            <p className="text-sm text-slate-500">Name</p>
            <p className="mt-2 text-xl font-semibold text-slate-900">{user?.fullName}</p>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
            <p className="text-sm text-slate-500">Role</p>
            <p className="mt-2 text-xl font-semibold text-slate-900">{user?.role}</p>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5 md:col-span-2">
            <p className="text-sm text-slate-500">Email</p>
            <p className="mt-2 text-xl font-semibold text-slate-900">{user?.email}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

export function AppRouter() {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 text-slate-600">
        Loading session...
      </div>
    );
  }

  if (!user) {
    return <LoginPage />;
  }

  return <DashboardPage />;
}
