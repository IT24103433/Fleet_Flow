CREATE TABLE "__FleetFlowServiceSchema" (
    version integer PRIMARY KEY,
    service_name text NOT NULL,
    checksum text NOT NULL,
    applied_at timestamptz NOT NULL DEFAULT NOW()
);
