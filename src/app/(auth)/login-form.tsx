"use client";

import { signIn } from "next-auth/react";
import { useState } from "react";

export function LoginForm() {
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  async function submit(formData: FormData) {
    setPending(true); setError("");
    const result = await signIn("credentials", { email: formData.get("email"), password: formData.get("password"), redirect: false });
    if (result?.error) { setError("Email hoac mat khau khong dung."); setPending(false); return; }
    window.location.assign("/moi-gioi");
  }
  return <form action={submit} className="mt-6 space-y-4">
    <label className="block text-sm font-medium">Email<input required name="email" type="email" autoComplete="email" className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5 outline-none focus:border-[#176b5c]" /></label>
    <label className="block text-sm font-medium">Mat khau<input required name="password" type="password" autoComplete="current-password" className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5 outline-none focus:border-[#176b5c]" /></label>
    {error ? <p role="alert" className="text-sm text-red-700">{error}</p> : null}
    <button disabled={pending} className="w-full rounded-lg bg-[#176b5c] px-4 py-3 font-semibold text-white disabled:opacity-60">{pending ? "Dang dang nhap..." : "Dang nhap"}</button>
  </form>;
}
