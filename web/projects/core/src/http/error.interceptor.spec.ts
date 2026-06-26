import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, throwError } from 'rxjs';

import type { ApiError } from '../domain/api-error';

import { errorNormalizationInterceptor, normalizeHttpError } from './error.interceptor';

const URL = '/api/public/recommend';

function setup(): { http: HttpClient; ctrl: HttpTestingController } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([errorNormalizationInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  return {
    http: TestBed.inject(HttpClient),
    ctrl: TestBed.inject(HttpTestingController),
  };
}

/** Issue a request, flush the given failure, and return the typed error the caller received. */
async function failWith(
  status: number,
  body: Record<string, unknown> | null,
  statusText = '',
): Promise<ApiError> {
  const { http, ctrl } = setup();
  const call = firstValueFrom(http.get(URL));
  const req = ctrl.expectOne(URL);
  req.flush(body, { status, statusText });
  try {
    await call;
    throw new Error('expected the request to fail');
  } catch (error) {
    ctrl.verify();
    return error as ApiError;
  }
}

describe('errorNormalizationInterceptor', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('maps a 400 { error, message } envelope to a validation ApiError preserving code/message', async () => {
    const err = await failWith(400, {
      error: 'Recommend.NoItems',
      message: 'Select at least one item.',
    });
    expect(err.kind).toBe('validation');
    expect(err.status).toBe(400);
    expect(err.code).toBe('Recommend.NoItems');
    expect(err.message).toBe('Select at least one item.');
    expect(err.messageKey).toBe('errors.validation');
  });

  it('maps a 401 to an unauthorized ApiError', async () => {
    const err = await failWith(401, { error: 'Auth.Unauthorized', message: 'Sign in required.' });
    expect(err.kind).toBe('unauthorized');
    expect(err.status).toBe(401);
    expect(err.messageKey).toBe('errors.unauthorized');
  });

  it('maps a 403 to a forbidden ApiError', async () => {
    const err = await failWith(403, { error: 'Auth.Forbidden', message: 'Admin only.' });
    expect(err.kind).toBe('forbidden');
    expect(err.messageKey).toBe('errors.forbidden');
  });

  it('maps a 404 to a not-found ApiError', async () => {
    const err = await failWith(404, { error: 'Restaurant.NotFound', message: 'No such venue.' });
    expect(err.kind).toBe('not-found');
    expect(err.status).toBe(404);
    expect(err.code).toBe('Restaurant.NotFound');
    expect(err.messageKey).toBe('errors.notFound');
  });

  it('maps a 500 to a server ApiError with a fallback code when no envelope is present', async () => {
    const err = await failWith(500, null, 'Internal Server Error');
    expect(err.kind).toBe('server');
    expect(err.status).toBe(500);
    expect(err.code).toBe('Http.ServerError');
    expect(err.message).toBe('Internal Server Error');
    expect(err.messageKey).toBe('errors.server');
  });

  it('maps a transport failure (status 0) to a network ApiError', async () => {
    const { http, ctrl } = setup();
    const call = firstValueFrom(http.get(URL));
    const req = ctrl.expectOne(URL);
    req.error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    let err: ApiError | undefined;
    try {
      await call;
    } catch (e) {
      err = e as ApiError;
    }
    ctrl.verify();
    expect(err?.kind).toBe('network');
    expect(err?.status).toBe(0);
    expect(err?.code).toBe('Http.Network');
    expect(err?.messageKey).toBe('errors.network');
  });

  it('normalizes a non-HttpErrorResponse failure into the typed model via the network branch', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            // The error-normalization interceptor is outer here; the inner interceptor throws a raw
            // (non-HttpErrorResponse) value, so the guarded cast must still produce a typed ApiError.
            errorNormalizationInterceptor,
            () => throwError(() => 'raw transport blowup'),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    const http = TestBed.inject(HttpClient);
    let err: ApiError | undefined;
    try {
      await firstValueFrom(http.get(URL));
    } catch (e) {
      err = e as ApiError;
    }
    expect(err?.kind).toBe('network');
    expect(err?.status).toBe(0);
    expect(err?.code).toBe('Http.Network');
    expect(err?.messageKey).toBe('errors.network');
  });

  it('never leaks a raw HttpErrorResponse — the caller receives the typed model', async () => {
    const err = await failWith(400, { error: 'X', message: 'Y' });
    // The typed model shape — not an HttpErrorResponse.
    expect(Object.keys(err).sort()).toEqual(['code', 'kind', 'message', 'messageKey', 'status']);
  });
});

describe('normalizeHttpError (pure mapping reused by the interceptor)', () => {
  it('falls back to the per-kind default code when the body is not an envelope', () => {
    const err = normalizeHttpError({ status: 404, statusText: 'Not Found', error: 'plain text' });
    expect(err.kind).toBe('not-found');
    expect(err.code).toBe('Http.NotFound');
    expect(err.message).toBe('Not Found');
  });

  it('maps an out-of-range status to unknown', () => {
    const err = normalizeHttpError({ status: 418, statusText: "I'm a teapot", error: null });
    expect(err.kind).toBe('unknown');
    expect(err.messageKey).toBe('errors.unknown');
  });
});
