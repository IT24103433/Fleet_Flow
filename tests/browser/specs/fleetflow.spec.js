// Assigned owner: Varun K. (IT24103696). Real network/application E2E tests.
import { test, expect } from '@playwright/test';

test.describe('Varun K. - FleetFlow UI/E2E', () => {
  test.afterEach(async ({ page }, info) => {
    info.annotations.push({ type:'assigned-owner', description:'Varun K. (IT24103696)' });
    if (info.status === 'passed') await info.attach('journey-result', { body:await page.screenshot(), contentType:'image/png' });
  });
  async function fillLogin(page, username='customer', password=process.env.ASE_TEST_PASSWORD) {
    const form=page.locator('form');
    await form.getByLabel(/Username|Work Email/).fill(username);
    await form.getByLabel(/^Password/).fill(password);
    await form.getByRole('button', { name:/^Sign in$/i }).click();
  }
  test('empty login validates required fields before submission', async ({ page }) => {
    await page.goto('/#/login');
    await page.locator('form').getByLabel(/Username or Email/).waitFor();
    await page.locator('form').getByRole('button', { name:/^Sign in$/i }).click();
    await expect(page.getByText('Username or email is required.', { exact:true })).toBeVisible();
    await expect(page.getByText('Password is required.', { exact:true })).toBeVisible();
    await expect(page.locator('form').getByLabel(/Username or Email/)).toBeFocused();
  });
  test('invalid credentials produce the real API rejection', async ({ page }) => {
    await page.goto('/#/login');
    const response = page.waitForResponse(r => r.url().endsWith('/api/auth/login') && r.request().method()==='POST');
    await fillLogin(page, 'customer', 'DeliberatelyWrong_Assessment_Password');
    expect((await response).status()).toBe(401);
    await expect(page.getByText('Invalid username/email or password.', { exact:true })).toBeVisible();
    await expect(page).toHaveURL(/#\/login/);
  });
  test('customer authenticates and sees their booking workspace', async ({ page }) => {
    await page.goto('/#/login');
    const response = page.waitForResponse(r => r.url().endsWith('/api/auth/login') && r.request().method()==='POST');
    await fillLogin(page);
    expect((await response).status()).toBe(200);
    await expect(page).toHaveURL(/#\/customer-home/);
    await expect(page.getByRole('heading', { name:'My bookings', exact:true })).toBeVisible();
    await expect(page.getByRole('heading', { name:'Good to see you, Customer User.' })).toBeVisible();
    await expect(page.getByText('Your next journey starts with a vehicle.', { exact:true })).toBeVisible();
  });
  test('customer is denied access through the staff login portal', async ({ page }) => {
    await page.goto('/#/staff-login');
    const response = page.waitForResponse(r => r.url().endsWith('/api/auth/login') && r.request().method()==='POST');
    await fillLogin(page);
    expect((await response).status()).toBe(403);
    await expect(page.getByText('Access Denied', { exact:true })).toBeVisible();
    await expect(page).toHaveURL(/#\/staff-login/);
  });
  test('catalogue filter, empty state, reset and vehicle detail use real seeded data', async ({ page }) => {
    await page.goto('/#/browse');
    await expect(page.getByRole('heading', { name:'ASE Roadster', exact:true })).toBeVisible();
    await page.getByLabel('Make, model, or keyword').fill('no-such-assessment-vehicle');
    await expect(page.getByRole('heading', { name:'No vehicles match these filters.', exact:true })).toBeVisible();
    await page.getByRole('button', { name:'Clear filters', exact:true }).click();
    await expect(page.getByRole('heading', { name:'ASE Roadster', exact:true })).toBeVisible();
    const card = page.locator('article').filter({ has:page.getByRole('heading', { name:'ASE Roadster', exact:true }) });
    await card.getByRole('button', { name:'View details' }).click();
    await expect(page).toHaveURL(/#\/vehicle-details\?vehicleId=/);
    await expect(page.getByRole('heading', { name:'ASE Roadster', exact:true, level:1 })).toBeVisible();
    await expect(page.getByText('Assessment hub', { exact:false }).first()).toBeVisible();
  });
});
