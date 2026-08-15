-- Phase 1+2 — Dictionary Schema Fix
-- Migration 003: Add IsActive column to SysReference
-- This column was added in the application code (MetadataGraph, SysReference model)
-- but was missing from the original migration DDL.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'sysreference'
          AND column_name = 'isactive'
    ) THEN
        ALTER TABLE "SysReference" ADD COLUMN "IsActive" BOOLEAN NOT NULL DEFAULT TRUE;
    END IF;
END $$;
