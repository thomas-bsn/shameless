import { type FormEvent, useId, useState } from "react";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

/** Les options d'une Série : son format, l'étiquette Fearless et l'alternance automatique des Sides. */
export interface SeriesOptions {
  format: number;
  fearless: boolean;
  alternateSides: boolean;
}

const formats = [1, 2, 3, 5] as const;

/** Le formulaire des options d'une nouvelle Série : validation du Picker et Revanche. */
export function SeriesOptionsForm({
  initial = { format: 3, fearless: false, alternateSides: false },
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
  const [alternateSides, setAlternateSides] = useState(initial.alternateSides);

  function submit(event: FormEvent) {
    event.preventDefault();
    onSubmit({ format: Number(format), fearless, alternateSides });
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor={`${id}-format`}>Format</Label>
        <Select value={format} onValueChange={setFormat}>
          <SelectTrigger id={`${id}-format`} aria-label="Format" className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {formats.map((f) => (
              <SelectItem key={f} value={String(f)}>
                BO{f}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox id={`${id}-fearless`} checked={fearless} onCheckedChange={(v) => setFearless(v === true)} />
        <Label htmlFor={`${id}-fearless`}>Fearless</Label>
      </div>
      <div className="flex items-center gap-2">
        <Checkbox
          id={`${id}-alternance`}
          checked={alternateSides}
          onCheckedChange={(v) => setAlternateSides(v === true)}
        />
        <Label htmlFor={`${id}-alternance`}>Alterner les Sides à chaque game</Label>
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
