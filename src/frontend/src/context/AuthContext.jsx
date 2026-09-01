import React, { createContext, useState, useContext, useEffect } from 'react';

const AuthContext = createContext(null);

const extractRolesFromToken = (token) => {
  try {
    if (!token || typeof token !== 'string') return [];
    const parts = token.split('.');
    if (parts.length < 2) return [];
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    const payload = JSON.parse(jsonPayload);
    const rawRole =
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
      payload['role'] ??
      [];

    if (Array.isArray(rawRole)) {
      return rawRole.filter((r) => typeof r === 'string' && r.trim().length > 0);
    }
    if (typeof rawRole === 'string' && rawRole.trim().length > 0) {
      return [rawRole.trim()];
    }
    return [];
  } catch {
    return [];
  }
};

export const AuthProvider = ({ children }) => {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const loadStoredAuth = () => {
      try {
        const storedToken = localStorage.getItem('token');
        const storedUser = localStorage.getItem('user');

        if (storedToken && storedUser) {
          const parsedUser = JSON.parse(storedUser);
          const roles = Array.isArray(parsedUser.roles)
            ? parsedUser.roles
            : extractRolesFromToken(storedToken);

          const userWithRoles = { ...parsedUser, roles };
          setToken(storedToken);
          setUser(userWithRoles);
          setIsAuthenticated(true);
        }
      } catch (error) {
        console.error("Error loading authentication state from localStorage:", error);
        // Clear corrupt data
        try {
          localStorage.removeItem('token');
          localStorage.removeItem('user');
        } catch {
          // ignore
        }
      } finally {
        setIsLoading(false);
      }
    };

    loadStoredAuth();
  }, []);

  const login = (newToken, newUser) => {
    try {
      const roles = extractRolesFromToken(newToken);
      const userWithRoles = { ...newUser, roles };

      localStorage.setItem('token', newToken);
      localStorage.setItem('user', JSON.stringify(userWithRoles));
      
      setToken(newToken);
      setUser(userWithRoles);
      setIsAuthenticated(true);
    } catch (error) {
      console.error("Error saving authentication state to localStorage:", error);
    }
  };

  const logout = () => {
    try {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      
      setToken(null);
      setUser(null);
      setIsAuthenticated(false);
    } catch (error) {
      console.error("Error removing authentication state from localStorage:", error);
    }
  };

  const roles = user?.roles || [];
  const hasRole = (roleName) => roles.includes(roleName);

  return (
    <AuthContext.Provider value={{ token, user, roles, hasRole, isAuthenticated, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
