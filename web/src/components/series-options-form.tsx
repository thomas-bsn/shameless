import { type FormEvent, useId, useState } from "react";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";

/** Les options d'une Série : son format et l'étiquette Fearless. */
export interface SeriesOptions {
  format: number;
  fearless: boolean;
}

const formats = [1, 2, 3, 5] as const;

/** Le formulaire des options d'une nouvelle Série : validation du Picker et Revanche. */
export function SeriesOptionsForm({
  initial = { format: 3, fearless: false },
  submitLabel,
  pending,
  error,
  onSubmit,
}: {
  initial?: SeriesOptions;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  onSubmit: (options: SeriesOptions) => void;
}) {
  const id = useId();
  const [format, setFormat] = useState(String(initial.format));
  const [fearless, setFearless] = useState(initial.fearless);

  function submit(event: FormEvent) {
    event.preventDefault();
    onSubmit({ format: Number(format), fearless });
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-2 text-sm leading-none font-medium">Format</legend>
        <div className="flex flex-wrap gap-2">
          {formats.map((f) => (
            <label
              key={f}
              className="flex h-9 min-w-14 cursor-pointer items-center justify-center rounded-md border px-3 text-sm font-medium has-checked:border-primary has-checked:bg-accent has-focus-visible:ring-2 has-focus-visible:ring-ring"
            >
              <input
                type="radio"
                name={`${id}-format`}
                value={f}
                checked={format === String(f)}
                onChange={() => setFormat(String(f))}
                className="sr-only"
              />
              BO{f}
            </label>
          ))}
        </div>
      </fieldset>
      <div className="flex items-center gap-2">
        <Checkbox id={`${id}-fearless`} checked={fearless} onCheckedChange={(v) => setFearless(v === true)} />
        <Label htmlFor={`${id}-fearless`}>Fearless</Label>
      </div>
      {error ? <ErrorAlert error={error} /> : null}
      <div>
        <Button type="submit" disabled={pending}>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
