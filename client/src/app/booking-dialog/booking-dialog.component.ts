import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { BookingApiService, toErrorMessage } from '../booking-api.service';
import { Booking, Room } from '../models';

/**
 * Modal for adding a booking. The form asks for one date and two times, so the
 * "same day" rule cannot be broken from the UI. The server still validates everything;
 * its error message is shown inside the dialog.
 */
@Component({
  selector: 'app-booking-dialog',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './booking-dialog.component.html',
})
export class BookingDialogComponent implements OnInit {
  @Input({ required: true }) rooms: Room[] = [];
  @Input({ required: true }) roomId!: number;
  @Input({ required: true }) date!: string;

  @Output() created = new EventEmitter<Booking>();
  @Output() closed = new EventEmitter<void>();

  private api = inject(BookingApiService);
  private fb = inject(FormBuilder);

  form = this.fb.nonNullable.group({
    roomId: [0, Validators.required],
    title: ['', Validators.required],
    date: ['', Validators.required],
    startTime: ['09:00', Validators.required],
    endTime: ['10:00', Validators.required],
    bookedBy: ['', Validators.required],
  });

  /**
   * Times every 15 minutes: "00:00", "00:15", ... "23:45".
   * A list instead of <input type="time">, because the browser's time field orders
   * hours and minutes by the computer's region settings (on some machines minutes come first).
   */
  readonly timeOptions = Array.from({ length: 24 * 4 }, (_, i) => {
    const hh = String(Math.floor(i / 4)).padStart(2, '0');
    const mm = String((i % 4) * 15).padStart(2, '0');
    return `${hh}:${mm}`;
  });

  saving = false;
  error = '';

  ngOnInit(): void {
    this.form.patchValue({ roomId: this.roomId, date: this.date });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.saving = true;
    this.error = '';

    this.api
      .create({
        roomId: v.roomId,
        title: v.title,
        start: `${v.date}T${v.startTime}:00`,
        end: `${v.date}T${v.endTime}:00`,
        bookedBy: v.bookedBy,
      })
      .subscribe({
        next: (booking) => this.created.emit(booking),
        error: (err) => {
          this.error = toErrorMessage(err);
          this.saving = false;
        },
      });
  }

  /** True when a required field was touched and left empty. */
  missing(field: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[field];
    return c.touched && c.hasError('required');
  }
}
