#pragma once
#include <filesystem>

namespace aoe {
// Runs before GUI/settings construction. Never creates a Tracer or debugger session.
// Exit 0: the requested static/live checks passed; 2: a validation gate failed;
// 3: the canonical diagnostic report could not be persisted.
int RunAoeDiagnostic(const std::filesystem::path& projectRoot,bool offline);
}
