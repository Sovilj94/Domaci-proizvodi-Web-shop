import { cn } from "@/lib/utils";

type Variant = "primary" | "secondary" | "ghost" | "danger" | "honey";
type Size = "sm" | "md" | "lg" | "icon";

/*
 * Pilula, ne zaobljeni pravougaonik. Uz serif naslove i krem podloge oštriji radijus
 * čita kao aplikacija, a ne kao prodavnica — cijeli predložak stoji na punom radijusu.
 */
const base =
  "inline-flex items-center justify-center gap-2 rounded-full font-medium transition active:scale-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2 focus-visible:ring-offset-cream disabled:pointer-events-none disabled:opacity-50";

const variants: Record<Variant, string> = {
  primary: "bg-brand-600 text-white shadow-sm hover:bg-brand-700",
  secondary: "bg-cream text-ink border border-line hover:bg-sand",
  ghost: "text-ink hover:bg-sand",
  danger: "bg-red-700 text-white hover:bg-red-800",
  /* Amber ispuna nosi TAMAN tekst — bijelo na #c17f2a daje 2.9:1 i pada AA. */
  honey: "bg-honey-500 text-ink hover:bg-honey-600",
};

const sizes: Record<Size, string> = {
  sm: "h-9 px-4 text-sm",
  md: "h-11 px-5 text-sm",
  lg: "h-12 px-7 text-base",
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
