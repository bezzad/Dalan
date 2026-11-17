/** CLI-08: the wait before the next try. It doubles after each failure, up to a maximum, and starts again after a success. */
export class Backoff {
  private readonly min: number;
  private readonly max: number;
  private wait: number;

  constructor(minSeconds: number, maxSeconds: number) {
    this.min = minSeconds;
    this.max = Math.max(minSeconds, maxSeconds);
    this.wait = minSeconds;
  }

  /** Seconds to wait now. */
  next(): number {
    const wait = this.wait;
    this.wait = Math.min(this.wait * 2, this.max);
    return wait;
  }

  reset(): void {
    this.wait = this.min;
  }
}
