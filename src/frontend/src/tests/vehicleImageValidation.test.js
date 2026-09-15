import { test, describe } from 'node:test';
import assert from 'node:assert/strict';
import {
  SUPPORTED_IMAGE_TYPES,
  MAX_IMAGE_SIZE_BYTES,
  canUserManageVehicleImages,
  validateVehicleImageFile,
  validateImageActionAuthorization,
} from '../validation/vehicleImageValidation.js';

describe('Vehicle Image Validation & Role Authorization Rules', () => {
  test('constants specify 5MB max size and expected MIME types', () => {
    assert.equal(MAX_IMAGE_SIZE_BYTES, 5242880);
    assert.deepEqual(SUPPORTED_IMAGE_TYPES, ['image/jpeg', 'image/png', 'image/webp']);
  });

  test('canUserManageVehicleImages permits ADMIN and FLEET_MANAGER only', () => {
    assert.equal(canUserManageVehicleImages(['ADMIN']), true);
    assert.equal(canUserManageVehicleImages(['FLEET_MANAGER']), true);
    assert.equal(canUserManageVehicleImages(['fleet_manager']), true);
    assert.equal(canUserManageVehicleImages(['MAINTENANCE_STAFF']), false);
    assert.equal(canUserManageVehicleImages(['CUSTOMER']), false);
    assert.equal(canUserManageVehicleImages([]), false);
  });

  test('validateImageActionAuthorization returns success for authorized roles and error for unauthorized', () => {
    const okAdmin = validateImageActionAuthorization(['ADMIN']);
    assert.equal(okAdmin.isValid, true);
    assert.equal(okAdmin.error, null);

    const failStaff = validateImageActionAuthorization(['MAINTENANCE_STAFF']);
    assert.equal(failStaff.isValid, false);
    assert.match(failStaff.error, /Only Fleet Managers and Administrators/);
  });

  test('validateVehicleImageFile accepts valid JPEG, PNG, and WebP under 5MB', () => {
    const jpegFile = { size: 1024 * 1024, type: 'image/jpeg' };
    assert.equal(validateVehicleImageFile(jpegFile).isValid, true);

    const pngFile = { size: 2 * 1024 * 1024, type: 'image/png' };
    assert.equal(validateVehicleImageFile(pngFile).isValid, true);

    const webpFile = { size: 500 * 1024, type: 'image/webp' };
    assert.equal(validateVehicleImageFile(webpFile).isValid, true);
  });

  test('validateVehicleImageFile rejects files larger than 5MB', () => {
    const largeFile = { size: 5 * 1024 * 1024 + 1, type: 'image/jpeg' };
    const res = validateVehicleImageFile(largeFile);
    assert.equal(res.isValid, false);
    assert.match(res.error, /exceeds the 5 MB limit/);
  });

  test('validateVehicleImageFile rejects unsupported MIME types', () => {
    const gifFile = { size: 500 * 1024, type: 'image/gif' };
    const resGif = validateVehicleImageFile(gifFile);
    assert.equal(resGif.isValid, false);
    assert.match(resGif.error, /Unsupported file format/);

    const pdfFile = { size: 100 * 1024, type: 'application/pdf' };
    const resPdf = validateVehicleImageFile(pdfFile);
    assert.equal(resPdf.isValid, false);
    assert.match(resPdf.error, /Unsupported file format/);

    const svgFile = { size: 100 * 1024, type: 'image/svg+xml' };
    const resSvg = validateVehicleImageFile(svgFile);
    assert.equal(resSvg.isValid, false);
    assert.match(resSvg.error, /Unsupported file format/);
  });

  test('validateVehicleImageFile rejects null or missing file', () => {
    const res = validateVehicleImageFile(null);
    assert.equal(res.isValid, false);
    assert.match(res.error, /No image file was provided/);
  });
});
