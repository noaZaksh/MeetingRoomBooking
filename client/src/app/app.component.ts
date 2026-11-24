import { Component, OnInit, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BookingApiService, toDateString, toErrorMessage } from './booking-api.service';
import { BookingDialogComponent } from './booking-dialog/booking-dialog.component';
import { Booking, Room } from './models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule, DatePipe, BookingDialogComponent],
  templateUrl: './app.component.html',
})
export class AppComponent implements OnInit {
  private api = inject(BookingApiService);

  rooms: Room[] = [];
  roomId: number | null = null;
  date = toDateString(new Date());

  bookings: Booking[] = [];
  loading = false;
  error = '';
  dialogOpen = false;

  ngOnInit(): void {
    this.api.getRooms().subscribe({
      next: (rooms) => {
        this.rooms = rooms;
        this.roomId = rooms[0]?.id ?? null;
        this.loadBookings();
      },
      error: (err) => (this.error = toErrorMessage(err)),
    });
  }

  loadBookings(): void {
    if (this.roomId === null || !this.date) {
      this.bookings = [];
      return;
    }
    this.loading = true;
    this.error = '';
    // The server returns the list already sorted by start time.
    this.api.getBookings(this.roomId, this.date).subscribe({
      next: (bookings) => {
        this.bookings = bookings;
        this.loading = false;
      },
      error: (err) => {
        this.error = toErrorMessage(err);
        this.bookings = [];
        this.loading = false;
      },
    });
  }

  onCreated(booking: Booking): void {
    this.dialogOpen = false;
    // Show the day and room of the new booking (the user may have picked another one in the dialog).
    this.roomId = booking.roomId;
    this.date = booking.start.slice(0, 10);
    this.loadBookings();
  }

  cancel(booking: Booking): void {
    if (!confirm(`Cancel "${booking.title}"?`)) {
      return;
    }
    this.error = '';
    this.api.cancel(booking.id).subscribe({
      next: () => this.loadBookings(),
      error: (err) => {
        this.error = toErrorMessage(err);
        this.loadBookings(); // e.g. 404: someone else already cancelled it - refresh the list
      },
    });
  }
}
