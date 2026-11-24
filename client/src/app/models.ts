export interface Room {
  id: number;
  name: string;
}

/** Times are local wall-clock strings without time zone, e.g. "2026-11-24T09:30:00". */
export interface Booking {
  id: number;
  roomId: number;
  title: string;
  start: string;
  end: string;
  bookedBy: string;
}

export interface CreateBookingRequest {
  roomId: number;
  title: string;
  start: string;
  end: string;
  bookedBy: string;
}
