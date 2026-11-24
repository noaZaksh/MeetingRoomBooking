import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Booking, CreateBookingRequest, Room } from './models';

/** Thin wrapper over the server API. The dev server proxies /api to the .NET app (proxy.conf.json). */
@Injectable({ providedIn: 'root' })
export class BookingApiService {
  private http = inject(HttpClient);

  getRooms(): Observable<Room[]> {
    return this.http.get<Room[]>('/api/rooms');
  }

  /** @param date yyyy-MM-dd */
  getBookings(roomId: number, date: string): Observable<Booking[]> {
    return this.http.get<Booking[]>('/api/bookings', { params: { roomId, date } });
  }

  create(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>('/api/bookings', request);
  }

  cancel(id: number): Observable<void> {
    return this.http.delete<void>(`/api/bookings/${id}`);
  }
}

/** Shape of server errors (ASP.NET ProblemDetails). */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Turns any HTTP error into a message a user can read. */
export function toErrorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) {
    return 'Unexpected error.';
  }
  if (err.status === 0) {
    return 'Cannot reach the server. Is the API running?';
  }

  const problem = err.error as ProblemDetails | null;
  if (problem?.errors) {
    // Validation errors: one message per line.
    return Object.values(problem.errors).flat().join('\n');
  }
  if (problem?.detail) {
    return problem.title ? `${problem.title}\n${problem.detail}` : problem.detail;
  }
  return problem?.title ?? `Request failed (HTTP ${err.status}).`;
}

/** Local date as yyyy-MM-dd. (Not toISOString(), which converts to UTC and can return yesterday/tomorrow.) */
export function toDateString(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
