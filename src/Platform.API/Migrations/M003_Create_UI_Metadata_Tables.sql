-- Migration 003 — Create UI Metadata Tables
-- Phase 3: UI (Generic Forms, Grids, Lookups, Menus)
-- Creates: sys_window, sys_field_group, sys_tab, sys_field, sys_menu
-- Idempotent: YES (DO $$...$$ guards)
-- Dependencies: M001 (sys_table, sys_column, sys_process)

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_window') THEN

        CREATE TABLE public.sys_window (
            sys_window_id           SERIAL         PRIMARY KEY,
            column_name             VARCHAR(60)    NOT NULL,
            name                    VARCHAR(120)   NOT NULL,
            description             VARCHAR(255),
            help                    TEXT,
            default_tab_id          INT,
            access_level            SMALLINT       NOT NULL DEFAULT 3,
            is_view                 BOOLEAN        NOT NULL DEFAULT FALSE,
            entity_type             VARCHAR(20)    NOT NULL DEFAULT 'D',
            is_active               BOOLEAN        NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_window
            ADD CONSTRAINT uq_sys_window_column_name UNIQUE (column_name);

        CREATE INDEX ix_sys_window_is_active
            ON public.sys_window(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_window_sort_order
            ON public.sys_window(access_level);
    END IF;
END $$;

-- sys_field_group created before sys_tab because sys_tab FK to sys_window
-- must exist before sys_tab. sys_field_group depends on sys_tab, but we
-- create it here so that all tables are listed in dependency order.
-- (sys_field_group FK to sys_tab is created AFTER sys_tab exists.)

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_field_group') THEN

        CREATE TABLE public.sys_field_group (
            sys_field_group_id      SERIAL         PRIMARY KEY,
            sys_tab_id              INT            NOT NULL,
            column_name             VARCHAR(60)    NOT NULL,
            name                    VARCHAR(120)   NOT NULL,
            seq_no                  INT            NOT NULL DEFAULT 0,
            col_span                INT            NOT NULL DEFAULT 12,
            is_collapsed            BOOLEAN        NOT NULL DEFAULT FALSE,
            entity_type             VARCHAR(20)    NOT NULL DEFAULT 'D',
            is_active               BOOLEAN        NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_field_group
            ADD CONSTRAINT fk_sys_field_group_tab
                FOREIGN KEY (sys_tab_id)
                REFERENCES public.sys_tab(sys_tab_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_field_group
            ADD CONSTRAINT uq_sys_field_group_tab_column
                UNIQUE (sys_tab_id, column_name);

        CREATE INDEX ix_sys_field_group_tab
            ON public.sys_field_group(sys_tab_id);

        CREATE INDEX ix_sys_field_group_is_active
            ON public.sys_field_group(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_field_group_seq_no
            ON public.sys_field_group(sys_tab_id, seq_no);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_tab') THEN

        CREATE TABLE public.sys_tab (
            sys_tab_id              SERIAL         PRIMARY KEY,
            sys_window_id           INT            NOT NULL,
            sys_table_id            INT            NOT NULL,
            column_name             VARCHAR(60)    NOT NULL,
            name                    VARCHAR(120)   NOT NULL,
            seq_no                  INT            NOT NULL DEFAULT 0,
            is_default_tab          BOOLEAN        NOT NULL DEFAULT FALSE,
            is_grid                 BOOLEAN        NOT NULL DEFAULT FALSE,
            where_clause            VARCHAR(500),
            is_deleteable           BOOLEAN        NOT NULL DEFAULT TRUE,
            entity_type             VARCHAR(20)    NOT NULL DEFAULT 'D',
            is_active               BOOLEAN        NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT fk_sys_tab_window
                FOREIGN KEY (sys_window_id)
                REFERENCES public.sys_window(sys_window_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT fk_sys_tab_table
                FOREIGN KEY (sys_table_id)
                REFERENCES public.sys_table(sys_table_id);

        ALTER TABLE public.sys_tab
            ADD CONSTRAINT uq_sys_tab_window_column
                UNIQUE (sys_window_id, column_name);

        CREATE INDEX ix_sys_tab_window
            ON public.sys_tab(sys_window_id);

        CREATE INDEX ix_sys_tab_table
            ON public.sys_tab(sys_table_id);

        CREATE INDEX ix_sys_tab_is_active
            ON public.sys_tab(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_tab_seq_no
            ON public.sys_tab(sys_window_id, seq_no);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_field') THEN

        CREATE TABLE public.sys_field (
            sys_field_id            SERIAL         PRIMARY KEY,
            sys_tab_id              INT            NOT NULL,
            sys_column_id           INT            NOT NULL,
            column_name             VARCHAR(60)    NOT NULL,
            name                    VARCHAR(120)   NOT NULL,
            control_type            VARCHAR(30)    NOT NULL,
            sys_field_group_id      INT,
            seq_no                  INT            NOT NULL DEFAULT 0,
            is_mandatory_override   BOOLEAN        NOT NULL DEFAULT FALSE,
            is_read_only_override   BOOLEAN        NOT NULL DEFAULT FALSE,
            col_span                INT            NOT NULL DEFAULT 1,
            row_span                INT            NOT NULL DEFAULT 1,
            display_logic           VARCHAR(500),
            read_only_logic         VARCHAR(500),
            mandatory_logic         VARCHAR(500),
            default_value           VARCHAR(255),
            entity_type             VARCHAR(20)    NOT NULL DEFAULT 'D',
            is_active               BOOLEAN        NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_tab
                FOREIGN KEY (sys_tab_id)
                REFERENCES public.sys_tab(sys_tab_id)
                ON DELETE CASCADE;

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_column
                FOREIGN KEY (sys_column_id)
                REFERENCES public.sys_column(sys_column_id);

        ALTER TABLE public.sys_field
            ADD CONSTRAINT fk_sys_field_group
                FOREIGN KEY (sys_field_group_id)
                REFERENCES public.sys_field_group(sys_field_group_id);

        ALTER TABLE public.sys_field
            ADD CONSTRAINT uq_sys_field_tab_column
                UNIQUE (sys_tab_id, sys_column_id);

        CREATE INDEX ix_sys_field_tab
            ON public.sys_field(sys_tab_id);

        CREATE INDEX ix_sys_field_column
            ON public.sys_field(sys_column_id);

        CREATE INDEX ix_sys_field_group
            ON public.sys_field(sys_field_group_id);

        CREATE INDEX ix_sys_field_is_active
            ON public.sys_field(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_field_seq_no
            ON public.sys_field(sys_tab_id, seq_no);
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables
                   WHERE table_schema = 'public'
                     AND table_name = 'sys_menu') THEN

        CREATE TABLE public.sys_menu (
            sys_menu_id             SERIAL         PRIMARY KEY,
            parent_id               INT,
            column_name             VARCHAR(60)    NOT NULL,
            name                    VARCHAR(120)   NOT NULL,
            icon                    VARCHAR(60),
            sequence                INT            NOT NULL DEFAULT 0,
            window_id               INT,
            process_id              INT,
            is_separator            BOOLEAN        NOT NULL DEFAULT FALSE,
            is_system               BOOLEAN        NOT NULL DEFAULT FALSE,
            entity_type             VARCHAR(20)    NOT NULL DEFAULT 'D',
            is_active               BOOLEAN        NOT NULL DEFAULT TRUE,
            created_by              INT,
            created_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
            updated_by              INT,
            updated_at              TIMESTAMPTZ    NOT NULL DEFAULT NOW()
        );

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_parent
                FOREIGN KEY (parent_id)
                REFERENCES public.sys_menu(sys_menu_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_window
                FOREIGN KEY (window_id)
                REFERENCES public.sys_window(sys_window_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT fk_sys_menu_process
                FOREIGN KEY (process_id)
                REFERENCES public.sys_process(sys_process_id);

        ALTER TABLE public.sys_menu
            ADD CONSTRAINT uq_sys_menu_column_name UNIQUE (column_name);

        CREATE INDEX ix_sys_menu_parent
            ON public.sys_menu(parent_id);

        CREATE INDEX ix_sys_menu_is_active
            ON public.sys_menu(is_active) WHERE is_active = TRUE;

        CREATE INDEX ix_sys_menu_sequence
            ON public.sys_menu(sequence);

        CREATE INDEX ix_sys_menu_window
            ON public.sys_menu(window_id);
    END IF;
END $$;

-- Seed data: sample window for development/testing
INSERT INTO public.sys_window (column_name, name, description, access_level, entity_type)
VALUES ('window_library_book', 'Library Book', 'Manage library book records', 3, 'D')
ON CONFLICT (column_name) DO NOTHING;
