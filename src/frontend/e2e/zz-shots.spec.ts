import { expect, test, type Page } from '@playwright/test';

const invNav = (page: Page) => page.getByRole('navigation', { name: 'Investigation sections' });

test('capture workspace chrome', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await expect(page.locator('#home-heading')).toBeVisible();
  await page.screenshot({ path: 'shots/budget-home.png' });
  await invNav(page).getByRole('button', { name: /^Evidence\b/ }).click();
  await expect(page.getByRole('heading', { name: /Evidence Inbox/i })).toBeVisible();
  await page.screenshot({ path: 'shots/budget-evidence.png' });
});

test('capture review desk desktop and mobile', async ({ page, request }) => {
  await request.post('/api/admin/seed', { data: { reset: true } });
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Review Desk\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claim Review Desk' })).toBeVisible();
  await expect(page.locator('.desk-claim-subject')).toBeVisible();
  await page.screenshot({ path: 'shots/review-desk-desktop.png' });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.desk-sheet-toggle').click();
  await expect(page.locator('.desk-queue-body')).toBeVisible();
  await page.screenshot({ path: 'shots/review-desk-mobile.png' });
});

test('capture identifier matches desktop and mobile', async ({ page, request }) => {
  await request.post('/api/admin/seed', { data: { reset: true } });
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await invNav(page).getByRole('button', { name: /^Matches\b/ }).click();
  await page.getByRole('button', { name: 'Recompute Proposals' }).click();
  await expect(page.locator('.match-card').first()).toBeVisible();
  await page.screenshot({ path: 'shots/matches-desktop.png', fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: 'shots/matches-mobile.png' });
});
