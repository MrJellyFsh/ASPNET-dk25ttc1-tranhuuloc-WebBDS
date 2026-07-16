"use server";

import { Role } from "@prisma/client";
import { hash } from "bcryptjs";
import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { z } from "zod";
import { authOptions } from "@/lib/auth";
import { prisma } from "@/lib/prisma";

const signupSchema = z.object({ name: z.string().trim().min(2).max(80), email: z.email().trim().toLowerCase(), password: z.string().min(8).max(100) });

export async function register(formData: FormData) {
  const parsed = signupSchema.safeParse({ name: formData.get("name"), email: formData.get("email"), password: formData.get("password") });
  if (!parsed.success) redirect("/dang-ky?error=invalid-input");
  const exists = await prisma.user.findUnique({ where: { email: parsed.data.email }, select: { id: true } });
  if (exists) redirect("/dang-nhap?error=email-exists");
  await prisma.user.create({ data: { name: parsed.data.name, email: parsed.data.email, passwordHash: await hash(parsed.data.password, 12), role: Role.CUSTOMER } });
  redirect("/dang-nhap?created=1");
}

export async function completeAgentOnboarding(formData: FormData) {
  const session = await getServerSession(authOptions);
  if (!session?.user?.id) redirect("/dang-nhap?next=/tro-thanh-moi-gioi");
  const agency = String(formData.get("agency") ?? "").trim();
  const bio = String(formData.get("bio") ?? "").trim();
  if (agency.length < 2 || agency.length > 120 || bio.length > 1000) redirect("/tro-thanh-moi-gioi?error=invalid-profile");
  await prisma.user.update({ where: { id: session.user.id }, data: { role: Role.AGENT, agentProfile: { upsert: { create: { agency, bio }, update: { agency, bio } } } } });
  // Signing in again refreshes the JWT with the upgraded role.
  redirect("/dang-nhap?agent=1");
}
