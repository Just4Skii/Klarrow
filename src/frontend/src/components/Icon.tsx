import type { SVGProps } from "react";

export type IconName =
  | "home"
  | "inbox"
  | "list"
  | "trace"
  | "audit"
  | "upload"
  | "alert"
  | "check"
  | "check-circle"
  | "question-circle"
  | "branch"
  | "x-circle"
  | "alert-diamond"
  | "dashed-circle"
  | "alert-triangle"
  | "copy"
  | "chevron-down"
  | "info"
  | "close"
  | "search"
  | "clock"
  | "external"
  | "file";

const paths: Record<IconName, string[]> = {
  home: ["M4.5 10.5 12 4l7.5 6.5", "M6.5 9.5V20h11V9.5", "M10 20v-5.5h4V20"],
  inbox: ["M4 6.5A1.5 1.5 0 0 1 5.5 5h13A1.5 1.5 0 0 1 20 6.5v11A1.5 1.5 0 0 1 18.5 19h-13A1.5 1.5 0 0 1 4 17.5Z", "M4 13.5h4l1.5 2.5h5L16 13.5h4"],
  list: ["M8 7h11", "M8 12h11", "M8 17h11", "M4.5 7h.01", "M4.5 12h.01", "M4.5 17h.01"],
  trace: ["M6 4.5v9a3.5 3.5 0 0 0 3.5 3.5H18", "m14.5 13 3.5 4-3.5 4", "M18 4.5h-6a3.5 3.5 0 0 0-3.5 3.5"],
  audit: ["M4.5 12a7.5 7.5 0 1 0 2.2-5.3", "M4.5 5v3.5H8", "M12 8v4.5l3 1.8"],
  upload: ["M12 15V4.5", "m8 8 4-3.5L16 8", "M5 15v3.5A1.5 1.5 0 0 0 6.5 20h11a1.5 1.5 0 0 0 1.5-1.5V15"],
  alert: ["M12 4.5 21 19H3Z", "M12 10v4", "M12 16.6h.01"],
  check: ["m5 12.5 4.5 4.5L19 7.5"],
  close: ["M6 6l12 12", "M18 6 6 18"],
  search: ["M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14Z", "m16 16 4 4"],
  clock: ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z", "M12 8v4.5l3 1.8"],
  external: ["M14 5h5v5", "M19 5l-7 7", "M18 13v5a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"],
  file: ["M7 3.5h6.5L18 8v12a.5.5 0 0 1-.5.5h-11A.5.5 0 0 1 6 20V4a.5.5 0 0 1 .5-.5Z", "M13 3.5V8h4.5"],
  "check-circle": ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z", "m8.5 12 2.3 2.3L15.5 9.5"],
  "question-circle": ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z", "M9.6 9.5a2.5 2.5 0 0 1 4.8.8c0 1.7-2.4 2-2.4 3.5", "M12 16.5h.01"],
  branch: ["M8 5v14", "M8 12h5a3 3 0 0 0 3-3V5", "M13 19a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z", "M16 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z"],
  "x-circle": ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z", "M8.7 8.7l6.6 6.6", "M15.3 8.7l-6.6 6.6"],
  "alert-diamond": ["M12 3.5 20.5 12 12 20.5 3.5 12Z", "M12 8.5v4.5", "M12 16.2h.01"],
  "dashed-circle": ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z"],
  "alert-triangle": ["M12 4.5 21 19H3Z", "M12 10v4", "M12 16.6h.01"],
  copy: ["M9 9V5.5A1.5 1.5 0 0 1 10.5 4h8A1.5 1.5 0 0 1 20 5.5v8a1.5 1.5 0 0 1-1.5 1.5H15", "M4 10.5A1.5 1.5 0 0 1 5.5 9h8a1.5 1.5 0 0 1 1.5 1.5v8a1.5 1.5 0 0 1-1.5 1.5h-8A1.5 1.5 0 0 1 4 18.5Z"],
  "chevron-down": ["m7 10 5 5 5-5"],
  info: ["M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16Z", "M12 11v5", "M12 8.2h.01"]
};

export default function Icon({ name, ...props }: { name: IconName } & SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false" {...props}>
      {paths[name].map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}
