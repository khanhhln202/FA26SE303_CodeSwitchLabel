import { useEffect, useRef, useState } from "react";
import { RotateCcw } from "lucide-react";

/**
 * Khung ký tên bằng chuột / ngón tay / bút cảm ứng (canvas + Pointer Events).
 * onChange(dataUrl | null): ảnh PNG của chữ ký sau mỗi nét, null khi xoá.
 */
export default function SignaturePad({ onChange, invalid = false, height = 140 }) {
  const canvasRef = useRef(null);
  const drawingRef = useRef(false);
  const lastRef = useRef(null);
  const [isEmpty, setIsEmpty] = useState(true);

  // Canvas theo đúng độ rộng hiển thị x devicePixelRatio để nét không bị mờ
  useEffect(() => {
    const canvas = canvasRef.current;
    const fit = () => {
      const ratio = window.devicePixelRatio || 1;
      const { width } = canvas.getBoundingClientRect();
      if (!width || canvas.width === Math.round(width * ratio)) return;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      const ctx = canvas.getContext("2d");
      ctx.scale(ratio, ratio);
      ctx.lineCap = "round";
      ctx.lineJoin = "round";
      ctx.lineWidth = 2.2;
      ctx.strokeStyle = "#16171C";
      // Đổi kích thước làm mất nét đã vẽ -> báo trống để người dùng ký lại
      setIsEmpty(true);
      onChange?.(null);
    };
    fit();
    const observer = new ResizeObserver(fit);
    observer.observe(canvas);
    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [height]);

  const pointOf = (e) => {
    const rect = canvasRef.current.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  };

  const handleDown = (e) => {
    e.preventDefault();
    canvasRef.current.setPointerCapture(e.pointerId);
    drawingRef.current = true;
    const p = pointOf(e);
    lastRef.current = p;
    // Chấm một điểm để chạm nhẹ cũng thành nét
    const ctx = canvasRef.current.getContext("2d");
    ctx.beginPath();
    ctx.arc(p.x, p.y, 1.1, 0, Math.PI * 2);
    ctx.fillStyle = "#16171C";
    ctx.fill();
  };

  const handleMove = (e) => {
    if (!drawingRef.current) return;
    const ctx = canvasRef.current.getContext("2d");
    const p = pointOf(e);
    const last = lastRef.current;
    ctx.beginPath();
    ctx.moveTo(last.x, last.y);
    // Đường cong qua trung điểm cho nét mượt
    const mid = { x: (last.x + p.x) / 2, y: (last.y + p.y) / 2 };
    ctx.quadraticCurveTo(last.x, last.y, mid.x, mid.y);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
    lastRef.current = p;
  };

  const handleUp = () => {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    setIsEmpty(false);
    onChange?.(canvasRef.current.toDataURL("image/png"));
  };

  const clear = () => {
    const canvas = canvasRef.current;
    canvas.getContext("2d").clearRect(0, 0, canvas.width, canvas.height);
    setIsEmpty(true);
    onChange?.(null);
  };

  return (
    <div>
      <div
        className={`relative rounded-xl border-[1.5px] border-dashed bg-white overflow-hidden transition-colors ${
          invalid ? "border-[var(--auth-error)]" : "border-[var(--auth-border-strong)]"
        }`}
      >
        <canvas
          ref={canvasRef}
          style={{ height, touchAction: "none" }}
          className="block w-full cursor-crosshair"
          onPointerDown={handleDown}
          onPointerMove={handleMove}
          onPointerUp={handleUp}
          onPointerCancel={handleUp}
          aria-label="Khung ký tên"
          role="img"
        />
        {/* Đường kẻ ký tên */}
        <div className="pointer-events-none absolute left-4 right-4 bottom-7 flex items-end gap-1.5 text-[#B7B4A9]">
          <span className="type-caption leading-none">✕</span>
          <span className="flex-1 border-b border-[#D8D5CB]" />
        </div>
        {/* Nút Ký lại nằm trong góc khung để không làm khung cao thêm */}
        {!isEmpty && (
          <button
            type="button"
            onClick={clear}
            className="absolute top-2 right-2 inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-white/90 border border-[var(--auth-border)] type-caption font-label text-[var(--auth-faint)] hover:text-[var(--auth-heading)] cursor-pointer"
          >
            <RotateCcw className="w-3 h-3" /> Ký lại
          </button>
        )}
        {isEmpty && (
          <p className="pointer-events-none absolute inset-x-0 top-1/2 -translate-y-1/2 text-center type-meta text-[#9AA8A6]">
            Ký tên bằng chuột hoặc ngón tay
          </p>
        )}
      </div>
    </div>
  );
}
