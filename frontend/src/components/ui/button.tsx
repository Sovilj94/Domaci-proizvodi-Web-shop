import { cn } from "@/lib/utils";

type Variant = "primary" | "istaknuto" | "secondary" | "bijelo" | "ghost" | "danger" | "honey";
type Size = "sm" | "md" | "lg" | "icon";

/*
 * Dugmad po uzoru na farmit (R1): pilula, 16px/600, `px-6 py-3`.
 *   primary   — narandžasto (farmit glavno dugme: „Odaberi", „Dodaj u korpu", „Prijavi se")
 *   istaknuto — zeleno (farmit „Popularno" kartica, zeleni CTA)
 *   secondary — bijelo sa narandžastim okvirom („Registruj se", „Pogledaj sve proizvode")
 *   bijelo    — bijela pilula preko fotografije (hero)
 * Bijeli tekst na #FE7E0F je ispod AA kontrasta — namjerno, isti je kao na uzoru.
 */
const base =
  "inline-flex items-center justify-center gap-2 rounded-full font-semibold transition active:scale-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-honey-500 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50";

const variants: Record<Variant, string> = {
  primary: "bg-honey-500 text-white hover:bg-honey-600",
  istaknuto: "bg-brand-600 text-white hover:bg-brand-700",
  secondary: "border border-honey-500 bg-white text-honey-500 hover:bg-honey-100",
  bijelo: "bg-white text-ink shadow-md hover:bg-honey-100",
  ghost: "text-ink hover:bg-sand",
  danger: "bg-red-700 text-white hover:bg-red-800",
  /* Stari naziv; od R1 isto što i primary. */
  honey: "bg-honey-500 text-white hover:bg-honey-600",
};

const sizes: Record<Size, string> = {
  sm: "h-9 px-4 text-sm",
  md: "h-11 px-6 text-[15px]",
  lg: "h-12 px-6 text-base",
  icon: "h-10 w-10",
};

export function buttonVariants({
  variant = "primary",
  size = "md",
}: { variant?: Variant; size?: Size } = {}): string {
  return cn(base, variants[variant], sizes[size]);
}

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
}

export function Button({
  variant = "primary",
  size = "md",
  className,
  ...props
}: ButtonProps) {
  return <button className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
