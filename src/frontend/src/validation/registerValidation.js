export const validateRegister = (values) => {
  const errors = {};

  // Full Name
  if (!values.fullName || values.fullName.trim() === '') {
    errors.fullName = "Full name is required.";
  } else if (values.fullName.trim().length < 2 || values.fullName.trim().length > 100) {
    errors.fullName = "Full name must be between 2 and 100 characters.";
  }

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

  // Phone Number
  if (!values.phoneNumber || values.phoneNumber.trim() === '') {
    errors.phoneNumber = "Phone number is required.";
  } else if (values.phoneNumber.trim().length < 7 || values.phoneNumber.trim().length > 20) {
    errors.phoneNumber = "Phone number must be between 7 and 20 characters.";
  } else if (!/^(\+?[0-9\s\-().]{7,20})$/.test(values.phoneNumber.trim())) {
    errors.phoneNumber = "Please enter a valid phone number.";
  }

  // Address
  if (!values.address || values.address.trim() === '') {
    errors.address = "Address is required.";
  } else if (values.address.trim().length < 5 || values.address.trim().length > 250) {
    errors.address = "Address must be between 5 and 250 characters.";
  }

  // Driving License Number
  if (!values.drivingLicenseNumber || values.drivingLicenseNumber.trim() === '') {
    errors.drivingLicenseNumber = "Driving license number is required.";
  } else if (values.drivingLicenseNumber.trim().length < 4 || values.drivingLicenseNumber.trim().length > 50) {
    errors.drivingLicenseNumber = "Driving license number must be between 4 and 50 characters.";
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
