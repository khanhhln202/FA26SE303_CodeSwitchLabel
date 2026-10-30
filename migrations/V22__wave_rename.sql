-- Rename the campaign domain to wave without moving or rewriting its data.
-- Historical migration files keep their original names and contents.
BEGIN;

DO $$
BEGIN
    IF to_regclass('public.campaign') IS NOT NULL
       AND to_regclass('public.wave') IS NULL THEN
        ALTER TABLE public.campaign RENAME TO wave;
    END IF;

    IF to_regclass('public.campaign_registration') IS NOT NULL
       AND to_regclass('public.wave_registration') IS NULL THEN
        ALTER TABLE public.campaign_registration RENAME TO wave_registration;
    END IF;

    IF to_regtype('public.campaign_status') IS NOT NULL
       AND to_regtype('public.wave_status') IS NULL THEN
        ALTER TYPE public.campaign_status RENAME TO wave_status;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'wave' AND column_name = 'campaign_id') THEN
        ALTER TABLE public.wave RENAME COLUMN campaign_id TO wave_id;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'wave' AND column_name = 'campaign_name') THEN
        ALTER TABLE public.wave RENAME COLUMN campaign_name TO wave_name;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'wave_registration' AND column_name = 'campaign_id') THEN
        ALTER TABLE public.wave_registration RENAME COLUMN campaign_id TO wave_id;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'task' AND column_name = 'campaign_id') THEN
        ALTER TABLE public.task RENAME COLUMN campaign_id TO wave_id;
    END IF;
END $$;

-- PostgreSQL updates view dependencies when the underlying tables/columns are renamed;
-- rename the exposed view and column names as well.
DO $$
DECLARE
    item RECORD;
    new_name TEXT;
BEGIN
    FOR item IN
        SELECT c.relname
          FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'public' AND c.relkind = 'v'
           AND c.relname ILIKE '%campaign%'
    LOOP
        new_name := replace(item.relname, 'campaign', 'wave');
        EXECUTE format('ALTER VIEW public.%I RENAME TO %I', item.relname, new_name);
    END LOOP;

    FOR item IN
        SELECT c.relname, a.attname
          FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
          JOIN pg_attribute a ON a.attrelid = c.oid
         WHERE n.nspname = 'public' AND c.relkind = 'v'
           AND a.attnum > 0 AND NOT a.attisdropped
           AND a.attname ILIKE '%campaign%'
    LOOP
        new_name := replace(item.attname, 'campaign', 'wave');
        EXECUTE format('ALTER VIEW public.%I RENAME COLUMN %I TO %I', item.relname, item.attname, new_name);
    END LOOP;
END $$;

-- Rename database functions and update their stored PL/pgSQL/SQL bodies. Variables such
-- as v_campaign are renamed too; they are local to the function and have no callers.
DO $$
DECLARE
    item RECORD;
    new_name TEXT;
    definition TEXT;
    old_ref TEXT;
BEGIN
    FOR item IN
        SELECT p.oid, p.proname
          FROM pg_proc p
          JOIN pg_namespace n ON n.oid = p.pronamespace
         WHERE n.nspname = 'public'
           AND p.prokind = 'f'
           AND (p.proname ILIKE '%campaign%' OR p.prosrc ILIKE '%campaign%')
         ORDER BY p.oid
    LOOP
        new_name := replace(item.proname, 'campaign', 'wave');
        IF new_name <> item.proname THEN
            old_ref := item.oid::regprocedure::text;
            EXECUTE format('ALTER FUNCTION %s RENAME TO %I', old_ref, new_name);
        END IF;

        definition := pg_get_functiondef(item.oid);
        definition := replace(definition, 'campaign', 'wave');
        definition := replace(definition, 'Campaign', 'Wave');
        definition := replace(definition, 'CAMPAIGN', 'WAVE');
        EXECUTE definition;
    END LOOP;
END $$;

-- Trigger names and arguments are stored separately from function bodies.
DO $$
DECLARE
    item RECORD;
    definition TEXT;
BEGIN
    FOR item IN
        SELECT t.oid, t.tgname, c.relname
          FROM pg_trigger t
          JOIN pg_class c ON c.oid = t.tgrelid
          JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'public' AND NOT t.tgisinternal
           AND (t.tgname ILIKE '%campaign%' OR encode(t.tgargs, 'escape') ILIKE '%campaign%')
    LOOP
        definition := pg_get_triggerdef(item.oid);
        definition := replace(definition, 'campaign', 'wave');
        definition := replace(definition, 'Campaign', 'Wave');
        definition := replace(definition, 'CAMPAIGN', 'WAVE');
        EXECUTE format('DROP TRIGGER %I ON public.%I', item.tgname, item.relname);
        EXECUTE definition;
    END LOOP;
END $$;

-- Constraint names, indexes, and identity sequences are not rewritten automatically.
DO $$
DECLARE
    item RECORD;
    new_name TEXT;
BEGIN
    FOR item IN
        SELECT con.conname, rel.relname
          FROM pg_constraint con
          JOIN pg_class rel ON rel.oid = con.conrelid
          JOIN pg_namespace n ON n.oid = rel.relnamespace
         WHERE n.nspname = 'public' AND con.conname ILIKE '%campaign%'
    LOOP
        new_name := replace(item.conname, 'campaign', 'wave');
        EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I', item.relname, item.conname, new_name);
    END LOOP;

    FOR item IN
        SELECT c.relname, c.relkind
          FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'public' AND c.relkind IN ('i', 'S')
           AND c.relname ILIKE '%campaign%'
           AND (c.relkind = 'S' OR NOT EXISTS (
               SELECT 1 FROM pg_constraint con WHERE con.conindid = c.oid))
    LOOP
        new_name := replace(item.relname, 'campaign', 'wave');
        IF item.relkind = 'i' THEN
            EXECUTE format('ALTER INDEX public.%I RENAME TO %I', item.relname, new_name);
        ELSE
            EXECUTE format('ALTER SEQUENCE public.%I RENAME TO %I', item.relname, new_name);
        END IF;
    END LOOP;
END $$;

COMMIT;
