import Link from "next/link";
import { Sprout } from "lucide-react";
import { cn } from "@/lib/utils";

export function Logo({ href = "/", className }: { href?: string; className?: string }) {
  return (
    <Link href={href} className={cn("inline-flex items-center gap-2", className)}>
      <span className="grid h-9 w-9 place-items-center rounded-full bg-brand-600 text-white shadow-sm">
        <Sprout className="h-5 w-5" />
      </span>
      <span className="font-display text-lg font-extrabold tracking-tight text-ink">
        E <span className="text-brand-600">piljara</span>
      </span>
    </Link>
  );
}
