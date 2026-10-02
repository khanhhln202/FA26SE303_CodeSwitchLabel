import { Suspense } from "react";
import { Routes } from "react-router-dom";

import { renderRoutes } from "./routes";
import Loading from "./components/Loading/Loading";

function App() {
  return (
    <Suspense fallback={<Loading className="min-h-screen" />}>
      <Routes>
        {renderRoutes()}
      </Routes>
    </Suspense>
  );
}

export default App;