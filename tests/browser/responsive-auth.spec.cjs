const { test, expect } = require('@playwright/test');
const pages = ['/Account/Login', '/Account/Register', '/Account/ForgotPassword',
  '/Account/ResetPassword?email=audit%40example.test&code=invalid', '/Account/ConfirmEmail'];
for (const width of [1440, 1280, 1024, 768, 430, 390, 375, 360, 844]) {
  for (const path of pages) {
    test(`${path.split('?')[0]} at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: width === 844 ? 390 : 900 });
      const response = await page.goto(path);
      expect(response.status()).toBe(200);
      await expect(page.locator('.auth-page')).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
      expect(await page.locator('.auth-v3-form-wrapper').evaluateAll(elements => elements.every(e => {
        const r = e.getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth + 1;
      }))).toBe(true);
      expect(await page.locator('.auth-page svg').evaluateAll(elements => elements.every(e => {
        const r = e.getBoundingClientRect(); return !r.width || (r.width <= 64 && r.height <= 64);
      }))).toBe(true);
      for (const form of await page.locator('form.auth-v3-form').all()) {
        await expect(form).toHaveAttribute('method', 'post');
        await expect(form.locator('input[name="__RequestVerificationToken"]')).toHaveCount(1);
      }
    });
  }
}
test('Auth CSS and submission/history scripts have valid response bodies', async ({ request }) => {
  for (const [path, type, marker] of [
    ['/css/auth.css', 'text/css', '.auth-v3-input-wrap'],
    ['/js/auth-forms.js', 'javascript', 'pageshow'],
  ]) {
    const response = await request.get(path);
    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain(type);
    expect(await response.text()).toContain(marker);
  }
});
test('History restoration clears outgoing auth animation and submission state', async ({ page }) => {
  await page.goto('/Account/Login');
  await page.evaluate(() => {
    document.querySelector('.auth-v3-shell').classList.add('auth-v3-leave-to-register');
    const form = document.querySelector('.auth-v3-form');
    form.dataset.submitting = 'true';
    form.querySelector('button[type="submit"]').disabled = true;
    window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }));
  });
  await expect(page.locator('button[type="submit"]')).toBeEnabled();
  await expect(page.locator('.auth-v3-shell')).not.toHaveClass(/auth-v3-leave-to-register/);
});
