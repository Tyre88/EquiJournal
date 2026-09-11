import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'ej-input',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EjInputComponent),
      multi: true
    }
  ],
  template: `
    <input
      class="input"
      [class.input--danger]="variant() === 'danger'"
      [type]="type()"
      [placeholder]="placeholder()"
      [disabled]="disabled()"
      [attr.name]="name()"
      [value]="value()"
      (input)="onInput($event)"
      (blur)="onTouched()"
    />
  `
})
export class EjInputComponent implements ControlValueAccessor {
  readonly type = input<'text' | 'email' | 'password' | 'number' | 'tel'>('text');
  readonly placeholder = input('');
  readonly variant = input<'default' | 'danger'>('default');
  readonly name = input('');

  protected disabled = signal(false);
  protected value = signal('');

  private onChange: (value: string) => void = () => {};
  protected onTouched: () => void = () => {};

  onInput(event: Event): void {
    const next = (event.target as HTMLInputElement).value;
    this.value.set(next);
    this.onChange(next);
  }

  writeValue(value: string): void {
    this.value.set(value ?? '');
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
