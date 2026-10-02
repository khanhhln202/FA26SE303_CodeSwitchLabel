import React, { useState, useEffect } from 'react';
import { Sun, Moon } from 'lucide-react';

export default function ThemeToggle() {
  const [darkMode, setDarkMode] = useState(() => {
    if (typeof window !== "undefined") {
      const savedTheme = localStorage.getItem("theme");
      if (savedTheme) return savedTheme === "dark";
      return document.documentElement.classList.contains("dark");
    }
    return true;
  });

  useEffect(() => {
    if (darkMode) {
      document.documentElement.classList.add("dark");
      localStorage.setItem("theme", "dark");
    } else {
      document.documentElement.classList.remove("dark");
      localStorage.setItem("theme", "light");
    }
  }, [darkMode]);

  const toggleDarkMode = () => {
    setDarkMode((prev) => !prev);
  };

  return (
    <button
      onClick={toggleDarkMode}
      type="button"
      className={`relative w-14 h-7 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-300 focus:outline-none shrink-0 ${
        darkMode ? "bg-slate-700 border border-slate-600" : "bg-slate-300"
      }`}
      title={darkMode ? "Gạt sang trái: Chế độ sáng" : "Gạt sang phải: Chế độ tối"}
    >
      <div
        className={`w-5 h-5 bg-white rounded-full shadow-md transform transition-transform duration-300 flex items-center justify-center ${
          darkMode ? "translate-x-7" : "translate-x-0"
        }`}
      >
        {darkMode ? (
          <Moon className="w-3.5 h-3.5 text-slate-900" />
        ) : (
          <Sun className="w-3.5 h-3.5 text-amber-500" />
        )}
      </div>
    </button>
  );
}