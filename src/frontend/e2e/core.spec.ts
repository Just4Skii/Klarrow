import { expect, test, type Page } from '@playwright/test';

const invNav = (page: Page) => page.getByRole('navigation', { name: 'Investigation sections' });

test('seeded trace flow works in the browser', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Klarrow', { exact: true })).toBeVisible();
  await expect(page.locator('#home-heading')).toBeVisible();

  await invNav(page).getByRole('button', { name: /^Evidence\b/ }).click();
  await expect(page.getByRole('heading', { name: /Evidence Inbox/i })).toBeVisible();

  await invNav(page).getByRole('button', { name: /^Traceability\b/ }).click();
  await page.getByRole('combobox').first().selectOption({ value: 'RM-0427' });
  await page.getByRole('button', { name: 'Run Traceability Analysis' }).click();

  await expect(page.getByText('Snapshot Reference')).toBeVisible();
  await expect(page.locator('.lot-pill', { hasText: 'FL-0514' })).toBeVisible();
  await expect(page.locator('.lot-pill', { hasText: 'FL-0515' })).toBeVisible();
  await expect(page.getByText('Tuckshop Arendse').first()).toBeVisible();
  await expect(page.getByRole('heading', { name: /Mass Balance/i })).toBeVisible();

  await expect(page.getByRole('link', { name: /Printable HTML Report/ })).toBeVisible();
});

test('claim review controls survive a refresh-free flow', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Claims\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claims & Observations' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Confirm', exact: true }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reject', exact: true }).first()).toBeVisible();
});

test('claims paginate client-side and open an honest source inspector', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Claims\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claims & Observations' })).toBeVisible();
  await expect(page.getByText(/Showing 1\D+25 of/)).toBeVisible();
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(page.getByText(/Page 2 of/)).toBeVisible();
  await page.getByRole('button', { name: 'Previous', exact: true }).click();
  await expect(page.getByText(/Page 1 of/)).toBeVisible();

  await page.getByRole('textbox', { name: 'Search claims' }).fill('.txt');
  await page.getByRole('button', { name: /· Line \d+/i }).first().click();
  const dialog = page.getByRole('dialog', { name: 'Source preview' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.source-lines li').first()).toBeVisible();
  await page.getByRole('button', { name: 'Close source preview' }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText(/Page 1 of/)).toBeVisible();
});

test('trace Paths, Graph, Table and link inspector use the saved result', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Traceability\b/ }).click();
  await page.getByRole('combobox').first().selectOption('RM-0427');
  await page.getByRole('button', { name: 'Run Traceability Analysis' }).click();
  await expect(page.getByText('Snapshot Reference')).toBeVisible();
  await expect(page.getByText('Unit conversion / additional records needed')).toBeVisible();

  const hop = page.locator('.hop').first();
  await expect(hop).toBeVisible();
  await hop.click();
  await page.getByRole('button', { name: 'Open link inspector' }).click();
  const dialog = page.getByRole('dialog', { name: 'Link inspector' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText(/was issued into production batch PB-2026-0912-A/)).toBeVisible();
  await expect(dialog.getByRole('heading', { name: 'Linked Source Citations' })).toBeVisible();
  await page.getByRole('button', { name: 'Close link inspector' }).click();
  await expect(dialog).toBeHidden();

  await page.getByRole('button', { name: 'Table', exact: true }).click();
  await expect(page.getByRole('columnheader', { name: 'Verification State' })).toBeVisible();
  await page.getByRole('button', { name: 'Graph', exact: true }).click();
  await expect(page.locator('.trace-graph')).toBeVisible();
  await page.getByRole('button', { name: 'Paths', exact: true }).click();
  await expect(hop).toBeVisible();
});

test('finding details drawer opens and closes with Escape', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Findings\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Reconciliation Findings' })).toBeVisible();
  await page.getByRole('button', { name: 'Inspect Details' }).first().click();
  const dialog = page.getByRole('dialog', { name: 'Finding details' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('Supporting Evidence Summary')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
});

test('claims use mobile card mode below 640px', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Claims\b/ }).click();
  await expect(page.locator('.mobile-cards')).toBeVisible();
  await expect(page.locator('.mobile-cards thead')).toBeHidden();
  await expect(page.locator('.mobile-cards tbody tr').first()).toBeVisible();
});

test('export review panel shows open items and disclaimer', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Traceability\b/ }).click();
  await page.getByRole('combobox').first().selectOption('RM-0427');
  await page.getByRole('button', { name: 'Run Traceability Analysis' }).click();
  await page.getByText('Snapshot Reference').waitFor();
  await invNav(page).getByRole('button', { name: /^Export Pack\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Export Traceability Package' })).toBeVisible();
  await expect(page.getByText(/open items noted|All items verified/)).toBeVisible();
  await expect(page.getByText(/Synthetic demonstration data active/).first()).toBeVisible();
});

test('global navigation and investigation scope are honest', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Command Center', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Investigations', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'System & AI', exact: true })).toBeVisible();
  await expect(page.getByText('Synthetic workspace', { exact: true })).toBeVisible();
  await expect(invNav(page)).toBeVisible();
  await invNav(page).getByRole('button', { name: /^Claims\b/ }).click();
  await expect(page.getByText(/Active Investigation Scope:/)).toBeVisible();
  await expect(page.locator('.locator-synthetic').first()).toBeVisible();
});

test('system page shows the honest model ladder (mocked endpoints)', async ({ page }) => {
  await page.route('**/api/extraction/status', (route) => route.fulfill({
    json: { isConfigured: true, mode: "configured", provider: "OpenAI-compatible Llama", modelId: "llama3.2:3b", endpoint: "http://127.0.0.1:11434/v1", message: "", setupInstructions: "", lastSuccessfulModelId: "", hasLiveExtractedFacts: false }
  }));
  await page.route('**/api/extraction/health', (route) => route.fulfill({
    json: { configured: true, reachable: true, baseUrl: "http://127.0.0.1:11434/v1", modelId: "llama3.2:3b", installedModels: ["llama3.2:3b"], error: "", checkedAt: new Date().toISOString() }
  }));
  await page.goto('/');
  await page.getByRole('button', { name: 'System & AI', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'System & AI Configuration' })).toBeVisible();
  await expect(page.getByText(/Installed Models:\s*llama3.2:3b/)).toBeVisible();
});

test('no horizontal page overflow at desktop and mobile widths', async ({ page }) => {
  for (const viewport of [{ width: 1440, height: 960 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow, `overflow at ${viewport.width}x${viewport.height}`).toBeLessThanOrEqual(0);
  }
});

test('operator can import evidence and see validation failures', async ({ page }) => {
  await page.goto('/');
  const allowed = `ref,lot,qty\nE2E-${Date.now()},RM-0427,5 kg\n`;
  await page.setInputFiles('#evidence-file', { name: 'e2e-import.csv', mimeType: 'text/csv', buffer: Buffer.from(allowed) });
  await expect(page.getByRole('status')).toContainText('candidate facts extracted');

  await page.setInputFiles('#evidence-file', { name: 'hostile.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('not allowed') });
  await expect(page.getByRole('alert')).toContainText(/unsupported file type/i);
});

test('confirming a match proposal records an alias without touching claims', async ({ page, request }) => {
  await request.post('/api/admin/seed', { data: { reset: true } });
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await invNav(page).getByRole('button', { name: /^Matches\b/ }).click();
  await expect(page.getByRole('heading', { name: /Identifier Matches & Aliases/i })).toBeVisible();
  await page.getByRole('button', { name: 'Recompute Proposals' }).click();

  const card = page.locator('.match-card').first();
  await expect(card.getByText('RM-O427', { exact: true }).first()).toBeVisible();
  await expect(card.locator('.comparison-list')).toBeVisible();
  await expect(card.locator('.consequence-line')).toContainText('source text is never altered');

  await card.getByRole('button', { name: 'Confirm Alias' }).click();
  const submit = card.getByRole('button', { name: 'Submit decision' });
  await expect(submit).toBeDisabled();
  await card.getByRole('textbox').fill('E2E: identifiers differ only by O/0 typography.');
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect(page.getByRole('status').first()).toContainText(/Alias confirmed/i);

  await page.getByRole('tab', { name: /^Aliases/ }).click();
  const aliasPanel = page.getByRole('tabpanel', { name: 'Confirmed aliases' });
  await expect(aliasPanel.getByText('RM-O427').first()).toBeVisible();
  await expect(aliasPanel.getByText('RM-0427').first()).toBeVisible();

  const factsResponse = await request.get('/api/facts');
  const facts = (await factsResponse.json()) as { reviewStatus: string }[];
  expect(facts.length).toBeGreaterThan(0);
  expect(facts.every((f) => f.reviewStatus === 'Unreviewed')).toBe(true);
});

test('operator can create an investigation and run a linked trace', async ({ page }) => {
  await page.goto('/');
  const name = `E2E recall ${Date.now()}`;
  await page.getByLabel('New investigation title').fill(name);
  await page.getByLabel('Case reference ID').fill('E2E-REF');
  await page.getByRole('button', { name: 'Create Case' }).click();
  await expect(page.getByRole('status')).toContainText('created');

  await invNav(page).getByRole('button', { name: /^Traceability\b/ }).click();
  await expect(page.getByText(new RegExp(`Case: ${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`))).toBeVisible();
  await page.getByRole('combobox').first().selectOption('RM-0427');
  await page.getByRole('button', { name: 'Run Traceability Analysis' }).click();
  await expect(page.getByText('Snapshot Reference')).toBeVisible();
  await expect(page.getByRole('link', { name: /Printable HTML Report/ })).toBeVisible();
});

test('deep link restores a saved trace run on reload', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Traceability\b/ }).click();
  await page.getByRole('combobox').first().selectOption('RM-0427');
  await page.getByRole('button', { name: 'Run Traceability Analysis' }).click();
  await expect(page.getByText('Snapshot Reference')).toBeVisible();
  await expect.poll(async () => page.url()).toContain('run=');
  await page.reload();
  await expect(page.getByText('Snapshot Reference')).toBeVisible();
  await expect(page.locator('.lot-pill', { hasText: 'FL-0514' })).toBeVisible();
  await expect(page.locator('.lot-pill', { hasText: 'FL-0515' })).toBeVisible();
});

test('unknown deep-link ids are ignored', async ({ page }) => {
  await page.goto('/#trace?run=00000000-0000-0000-0000-000000000000');
  await expect(page.locator('#home-heading').or(page.locator('#trace-heading')).first()).toBeVisible();
  await expect(page.getByText('Snapshot Reference')).toBeHidden();
});

test('finding drawer traps focus, closes on Escape and returns focus', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Findings\b/ }).click();
  const trigger = page.getByRole('button', { name: 'Inspect Details' }).first();
  await trigger.click();
  const dialog = page.getByRole('dialog', { name: 'Finding details' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button', { name: /close/i })).toBeFocused();
  for (let i = 0; i < 15; i++) { await page.keyboard.press('Tab'); }
  const inside = await page.evaluate(() => {
    const d = document.querySelector('[role="dialog"]');
    return !!d && !!document.activeElement && d.contains(document.activeElement);
  });
  expect(inside).toBe(true);
  for (let i = 0; i < 15; i++) { await page.keyboard.press('Shift+Tab'); }
  const stillInside = await page.evaluate(() => {
    const d = document.querySelector('[role="dialog"]');
    return !!d && !!document.activeElement && d.contains(document.activeElement);
  });
  expect(stillInside).toBe(true);
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});

test('review desk shortcuts move the queue and never submit a decision', async ({ page }) => {
  const mutations: string[] = [];
  page.on('request', (request) => {
    const url = request.url();
    if ((request.method() === 'POST' || request.method() === 'PATCH') && /\/api\/(facts|match-proposals|entity-aliases)/.test(url)) {
      mutations.push(`${request.method()} ${url}`);
    }
  });

  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Review Desk\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claim Review Desk' })).toBeVisible();

  const selectedId = () => page.locator('.desk-queue-item.is-selected').getAttribute('data-fact-id');
  const first = await selectedId();
  expect(first).toBeTruthy();

  await page.keyboard.press('j');
  await expect.poll(selectedId).not.toBe(first);
  await page.keyboard.press('k');
  await expect.poll(selectedId).toBe(first);

  for (const key of ['c', 'e', 'r']) {
    await page.keyboard.press(key);
    await expect(page.locator('#desk-decision-form')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.locator('#desk-decision-form')).toBeHidden();
  }

  const historyToggle = page.locator('.desk-history-toggle');
  await expect(historyToggle).toHaveAttribute('aria-expanded', 'false');
  await page.keyboard.press('h');
  await expect(historyToggle).toHaveAttribute('aria-expanded', 'true');
  await page.keyboard.press('h');
  await expect(historyToggle).toHaveAttribute('aria-expanded', 'false');

  await page.keyboard.press('?');
  const help = page.getByRole('dialog', { name: 'Keyboard shortcuts' });
  await expect(help).toBeVisible();
  await expect(help.getByText(/never confirm or reject a claim automatically/i)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(help).toBeHidden();

  expect(mutations).toEqual([]);
});

test('review desk requires a reason and records decisions in the append-only history', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Review Desk\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claim Review Desk' })).toBeVisible();

  const form = page.locator('#desk-decision-form');
  const result = page.locator('.desk-result');

  await page.keyboard.press('r');
  await expect(form).toBeVisible();
  await expect(form.getByRole('button', { name: 'Submit decision' })).toBeDisabled();
  await form.locator('textarea').fill('E2E rejection reason');
  await form.getByRole('button', { name: 'Submit decision' }).click();
  await expect(result).toContainText(/Rejected/);

  await page.keyboard.press('k');
  await page.keyboard.press('h');
  const history = page.locator('.history-list');
  await expect(history).toContainText('Rejected');
  await expect(history).toContainText('E2E rejection reason');

  await page.keyboard.press('e');
  await expect(form).toBeVisible();
  await expect(form.getByRole('button', { name: 'Save edit' })).toBeDisabled();
  await form.getByLabel('Extracted Value').fill('E2E-edited-raw-value');
  await form.locator('textarea').fill('E2E edit reason');
  await form.getByRole('button', { name: 'Save edit' }).click();
  await expect(result).toContainText(/Updated/);

  await page.keyboard.press('k');
  await expect(history).toContainText('Edited');
  await expect(history).toContainText('E2E edit reason');
  await expect(history).toContainText('E2E-edited-raw-value');
});

test('confirming a claim without a source locator requires a reason', async ({ page }) => {
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Review Desk\b/ }).click();

  const item = page.locator('.desk-queue-item', { hasText: '23_photo_supplier_label.bmp' }).first();
  await item.scrollIntoViewIfNeeded();
  await item.click();
  await expect(page.locator('.locator-card')).toContainText(/No line locator recorded/);

  await page.keyboard.press('c');
  const form = page.locator('#desk-decision-form');
  await expect(form).toBeVisible();
  await expect(form.getByText(/no line locator/i).first()).toBeVisible();
  await expect(form.getByRole('button', { name: 'Submit decision' })).toBeDisabled();
  await form.locator('textarea').fill('Locator is not recorded in the source; confirmed from the file name in the queue.');
  await form.getByRole('button', { name: 'Submit decision' }).click();
  await expect(page.locator('.desk-result')).toContainText(/Confirmed/);
});

test('review desk collapses the queue into a sheet with a sticky decision bar on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await invNav(page).getByRole('button', { name: /^Review Desk\b/ }).click();
  await expect(page.getByRole('heading', { name: 'Claim Review Desk' })).toBeVisible();

  const toggle = page.locator('.desk-sheet-toggle');
  await expect(toggle).toBeVisible();
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  await expect(page.locator('.desk-queue-body')).toBeHidden();
  await toggle.click();
  await expect(page.locator('.desk-queue-body')).toBeVisible();
  await expect(page.locator('.desk-queue-item').first()).toBeVisible();

  const bar = page.locator('.desk-mobile-bar');
  await expect(bar).toBeVisible();
  await expect(bar.getByRole('button', { name: /^Confirm/ })).toBeVisible();

  await page.keyboard.press('c');
  await expect(bar.getByRole('button', { name: 'Submit decision' })).toBeVisible();
  await expect(bar.getByRole('button', { name: 'Cancel' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(bar.getByRole('button', { name: /^Confirm/ })).toBeVisible();

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  expect(overflow).toBeLessThanOrEqual(0);
});

test('reduced motion keeps drawers functional', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  expect(await page.evaluate(() => window.matchMedia('(prefers-reduced-motion: reduce)').matches)).toBe(true);
  await invNav(page).getByRole('button', { name: /^Findings\b/ }).click();
  await page.getByRole('button', { name: 'Inspect Details' }).first().click();
  await expect(page.getByRole('dialog', { name: 'Finding details' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Finding details' })).toBeHidden();
});
