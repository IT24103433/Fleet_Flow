import React, { createContext, useState, useContext, useEffect } from 'react';
import { extractRolesFromToken, isTokenExpired } from '../utils/roleUtils';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const loadStoredAuth = () => {
      // 1. Purge legacy stale authentication keys from localStorage
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      } catch {
        // ignore storage access restrictions
      }

      // 2. Load active authentication session from sessionStorage
      try {
        const storedToken = sessionStorage.getItem('token');
        const storedUser = sessionStorage.getItem('user');

        if (storedToken && storedUser) {
          // 3. Validate token expiration
          if (isTokenExpired(storedToken)) {
            sessionStorage.removeItem('token');
            sessionStorage.removeItem('user');
          } else {
            const parsedUser = JSON.parse(storedUser);
            const roles = Array.isArray(parsedUser.roles)
              ? parsedUser.roles
              : extractRolesFromToken(storedToken);

            const userWithRoles = { ...parsedUser, roles };
            setToken(storedToken);
            setUser(userWithRoles);
            setIsAuthenticated(true);
          }
        }
      } catch (error) {
        console.error("Error loading authentication state from sessionStorage:", error);
        try {
          sessionStorage.removeItem('token');
          sessionStorage.removeItem('user');
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

      // Store in sessionStorage (active tab session only)
      sessionStorage.setItem('token', newToken);
      sessionStorage.setItem('user', JSON.stringify(userWithRoles));

      // Clean up legacy localStorage keys
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      } catch {
        // ignore
      }

      setToken(newToken);
      setUser(userWithRoles);
      setIsAuthenticated(true);
    } catch (error) {
      console.error("Error saving authentication state to sessionStorage:", error);
    }
  };

  const logout = () => {
    try {
      // Clear sessionStorage active session
      sessionStorage.removeItem('token');
      sessionStorage.removeItem('user');

      // Clear legacy localStorage keys
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
      } catch {
        // ignore
      }

      setToken(null);
      setUser(null);
      setIsAuthenticated(false);
    } catch (error) {
      console.error("Error removing authentication state from sessionStorage:", error);
    }
  };

  const updateUser = (partialUser) => {
    setUser((prevUser) => {
      if (!prevUser) return prevUser;
      const updated = { ...prevUser, ...partialUser };
      try {
        sessionStorage.setItem('user', JSON.stringify(updated));
      } catch (error) {
        console.error("Error updating user in sessionStorage:", error);
      }
      return updated;
    });
  };

  const roles = user?.roles || [];
  const hasRole = (roleName) => roles.includes(roleName);

  return (
    <AuthContext.Provider value={{ token, user, roles, hasRole, isAuthenticated, isLoading, login, logout, updateUser }}>
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
