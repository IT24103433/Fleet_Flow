export const validateLogin = (values) => {
  const errors = {};

  // Username or Email
  if (!values.usernameOrEmail || values.usernameOrEmail.trim() === '') {
    errors.usernameOrEmail = "Username or email is required.";
  }

  // Password
  if (!values.password || values.password.trim() === '') {
    errors.password = "Password is required.";
  }

  return errors;
};
