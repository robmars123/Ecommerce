// Test helpers: mock `fetch` with Jest only (no extra mocking packages).

export function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json', ...headers },
  });
}

export function problemResponse(status: number, body: Record<string, unknown>, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify({ status, ...body }), {
    status,
    headers: { 'content-type': 'application/problem+json', ...headers },
  });
}

export function mockFetchOnce(response: Response) {
  return jest.spyOn(globalThis, 'fetch').mockImplementationOnce(async () => response);
}

/** Routes by URL substring (longest key wins); every call is recorded on the returned spy. */
export function mockFetchRoutes(routes: Record<string, () => Response>) {
  return jest.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);
    const key = Object.keys(routes)
      .sort((a, b) => b.length - a.length)
      .find((k) => url.includes(k));
    if (!key) {
      return new Response('not mocked: ' + url, { status: 599 });
    }
    return routes[key]();
  });
}

export function calledUrls(spy: jest.SpyInstance): string[] {
  return spy.mock.calls.map((c) => String(c[0]));
}
