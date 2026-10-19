import { Children, cloneElement, isValidElement, useId } from "react";
import { cn } from "@/lib/utils";

export const fieldClass =
  "w-full rounded-xl border border-line bg-sand px-3.5 py-2.5 text-sm text-ink transition placeholder:text-muted/70 focus:border-brand-400 focus:bg-surface focus:outline-none focus:ring-2 focus:ring-brand-400 disabled:opacity-60";

export function Label({
  children,
  htmlFor,
  required,
}: {
  children: React.ReactNode;
  htmlFor?: string;
  required?: boolean;
}) {
  return (
    <label htmlFor={htmlFor} className="mb-1.5 block text-sm font-medium text-ink">
      {children}
      {required && <span className="text-red-500"> *</span>}
    </label>
  );
}

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cn(fieldClass, className)} {...props} />;
}

export function Textarea({
  className,
  ...props
}: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cn(fieldClass, "min-h-24 resize-y", className)} {...props} />;
}

export function Select({
  className,
  ...props
}: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={cn(fieldClass, "cursor-pointer", className)} {...props} />;
}

type PoljeProps = {
  id?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
};

/** Polja kojima `Field` smije sam dodijeliti `id` — ona ga sigurno prosljeđuju do elementa forme. */
const POLJA: unknown[] = ["input", "select", "textarea", Input, Select, Textarea];

/**
 * Labela, polje i greška ispod. E1: kad `htmlFor` nije zadat, a prvo dijete je polje bez `id`-a,
 * `Field` mu sam dodijeli `id` i veže labelu — tako sve forme koje ga koriste dobijaju klik na
 * labelu i ime polja za čitač ekrana bez prepravke svake forme. Greška se veže na polje kroz
 * `aria-describedby`. Za drugu komponentu (npr. birač adrese) `htmlFor` se i dalje zadaje ručno.
 */
export function Field({
  label,
  htmlFor,
  required,
  error,
  children,
}: {
  label: string;
  htmlFor?: string;
  required?: boolean;
  error?: string;
  children: React.ReactNode;
}) {
  const autoId = useId();
  const nizDjece = Children.toArray(children);
  const prvo = nizDjece[0];
  const polje = isValidElement<PoljeProps>(prvo) && POLJA.includes(prvo.type) ? prvo : null;

  const poljeId = htmlFor ?? polje?.props.id ?? (polje ? autoId : undefined);
  const greskaId = error && poljeId ? `${poljeId}-greska` : undefined;

  const djeca = polje
    ? [
        cloneElement(polje, {
          id: poljeId,
          "aria-invalid": error ? true : polje.props["aria-invalid"],
          "aria-describedby":
            [polje.props["aria-describedby"], greskaId].filter(Boolean).join(" ") || undefined,
        }),
        ...nizDjece.slice(1),
      ]
    : children;

  return (
    <div>
      <Label htmlFor={poljeId} required={required}>
        {label}
      </Label>
      {djeca}
      {error && (
        <p id={greskaId} className="mt-1 text-xs font-medium text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
