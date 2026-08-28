export const validateRegister = (values) => {
  const errors = {};

  // Username
  if (!values.username || values.username.trim() === '') {
    errors.username = "Username is required.";
  } else if (values.username.length < 3 || values.username.length > 50) {
    errors.username = "Username must be between 3 and 50 characters.";
  } else if (!/^[a-zA-Z0-9\-_]+$/.test(values.username)) {
    errors.username = "Username can only contain letters, numbers, hyphens, and underscores.";
  }

  // Email
  if (!values.email || values.email.trim() === '') {
    errors.email = "Email is required.";
  } else if (values.email.length > 100) {
    errors.email = "Email must not exceed 100 characters.";
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) {
    errors.email = "Email is not a valid email address.";
  }

  // Password
  if (!values.password) {
    errors.password = "Password is required.";
  } else if (values.password.length < 8) {
    errors.password = "Password must be at least 8 characters long.";
  } else {
    const hasUppercase = /[A-Z]/.test(values.password);
    const hasLowercase = /[a-z]/.test(values.password);
    const hasNumber = /\d/.test(values.password);
    const hasSpecial = /[^\da-zA-Z]/.test(values.password);

    if (!hasUppercase || !hasLowercase || !hasNumber || !hasSpecial) {
      errors.password = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.";
    }
  }

  // Confirm Password
  if (!values.confirmPassword) {
    errors.confirmPassword = "Confirm Password is required.";
  } else if (values.confirmPassword !== values.password) {
    errors.confirmPassword = "Passwords do not match.";
  }

  return errors;
};
