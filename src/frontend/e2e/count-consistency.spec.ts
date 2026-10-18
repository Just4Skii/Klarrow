import { expect, test, type APIRequestContext } from '@playwright/test';

const json = async <T>(request: APIRequestContext, url: string): Promise<T> =>
  (await (await request.get(url)).json()) as T;

type Doc = { processingStatus: string };
type Fact = { reviewStatus: string };
type Gap = { status: string };
type Match = { state: string };
type WorkspaceHome = { primaryInvestigationId?: string };

test('command center counts agree with API sources and the shared decision formula', async ({ page, request }) => {
  const home = await json<WorkspaceHome>(request, '/api/workspace/home');
  const scope = home.primaryInvestigationId ? `?investigationId=${home.primaryInvestigationId}` : '';
  const [documents, facts, findings, matches, runs, audits] = await Promise.all([
    json<Doc[]>(request, `/api/documents${scope}`),
    json<Fact[]>(request, `/api/workspace/facts${scope}`),
    json<Gap[]>(request, '/api/gaps'),
    json<Match[]>(request, '/api/match-proposals'),
    json<unknown[]>(request, '/api/trace-runs'),
    json<unknown[]>(request, '/api/workspace/audits')
  ]);

  const claimsPending = facts.filter((f) => f.reviewStatus === 'Unreviewed').length;
  const findingsOpen = findings.filter((f) => f.status === 'Open').length;
  const matchesCandidate = matches.filter((m) => m.state === 'Candidate').length;
  const attention = documents.filter((d) => d.processingStatus === 'parse_failed' || d.processingStatus === 'retained_not_ocr_processed').length;
  const openItems = claimsPending + matchesCandidate + findingsOpen;
  const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`;

  await page.goto('/');
  await page.waitForLoadState('networkidle');

  const metricValue = async (label: string) =>
    (await page.locator('.metric').filter({ hasText: label }).locator('.metric-value').innerText()).trim();
  expect(await metricValue('Evidence files'), 'evidence files tile').toBe(String(documents.length));
  expect(await metricValue('Extracted claims'), 'extracted claims tile').toBe(String(facts.length));
  expect(await metricValue('Pending review'), 'pending review tile').toBe(String(claimsPending));
  expect(await metricValue('Trace snapshots'), 'trace snapshots tile').toBe(String(runs.length));
  expect(await metricValue('Open findings'), 'open findings tile').toBe(String(findingsOpen));

  const gapChip = (await page.locator('section[aria-labelledby="gaps-heading"] .badge').first().innerText()).trim();
  expect(gapChip, 'findings panel chip').toBe(`${findingsOpen} open ${findingsOpen === 1 ? 'finding' : 'findings'}`);

  const step4 = (await page.locator('.workflow-step').nth(3).innerText()).replace(/\s+/g, ' ');
  if (runs.length > 0) {
    expect(step4, 'trace step summary').toContain(
      openItems > 0
        ? `${plural(runs.length, 'trace run')} · ${plural(openItems, 'open item')}`
        : `${plural(runs.length, 'trace run')} · no open items`
    );
  } else {
    expect(step4, 'trace step summary').toContain(openItems > 0 ? `No trace run yet · ${plural(openItems, 'open item')}` : 'No trace run yet');
  }

  if (!home.primaryInvestigationId) {
    await expect(page.getByRole('button', { name: 'Select an Investigation' })).toBeVisible();
    return;
  }

  const decisionLink = page.locator('.decision-link');
  if (openItems > 0) {
    await expect(decisionLink, 'decision pill').toHaveText(`Needs your decision: ${openItems}`);
  } else {
    await expect(decisionLink, 'decision pill').toHaveText('No decisions pending');
  }
  const formula = (await page.locator('#decision-formula').innerText()).trim();
  expect(formula, 'decision formula help text').toBe(
    `${plural(claimsPending, 'pending claim')} + ${plural(matchesCandidate, 'match proposal')} + ${plural(findingsOpen, 'finding')}`
  );

  const tab = (name: string) => page.locator('.investigation-tabs .nav-item').filter({ hasText: name });
  const expectBadge = async (name: string, count: number) => {
    const badge = tab(name).locator('.nav-badge');
    if (count > 0) await expect(badge, `${name} badge`).toHaveText(String(count));
    else await expect(badge, `${name} badge`).toHaveCount(0);
  };
  await expectBadge('Evidence', documents.length);
  await expectBadge('Review Desk', claimsPending);
  await expectBadge('Matches', matchesCandidate);
  await expectBadge('Findings', findingsOpen);
  await expectBadge('Audit Trail', audits.length);
  await expect(tab('Claims').locator('.nav-badge'), 'claims tab shows no duplicated decision count').toHaveCount(0);

  const expectedAction =
    attention > 0
      ? { label: 'Review Evidence Issues', reason: `check ${attention}` }
      : matchesCandidate > 0
        ? { label: 'Resolve Lot Matches', reason: `decide ${matchesCandidate}` }
        : claimsPending > 0
          ? { label: 'Open Review Desk', reason: `review ${claimsPending}` }
          : runs.length === 0
            ? { label: 'Run Lot Traceability', reason: 'no trace run saved' }
            : findingsOpen > 0
              ? { label: 'Review Open Findings', reason: `resolve ${findingsOpen}` }
              : { label: 'Export Review Pack', reason: 'export the reviewed investigation pack' };
  const heroActions = page.locator('.hero-actions');
  await expect(heroActions.getByRole('button', { name: expectedAction.label }), 'primary next action').toBeVisible();
  await expect(heroActions.locator('.hero-next-hint'), 'next action reason').toContainText(expectedAction.reason);
});
