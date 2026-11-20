-- Nhãn ngôn ngữ phải dính sát từ đầu tiên. Dữ liệu cũ có thể còn khoảng trắng
-- sau [vi]/[en] do người nhập tay; chuẩn hóa một lần để parser và hiển thị khớp nhau.
UPDATE script
   SET cs_content = regexp_replace(cs_content, '(\[(?:vi|en)\])\s+', '\1', 'g'),
       vi_content = regexp_replace(vi_content, '(\[(?:vi|en)\])\s+', '\1', 'g')
 WHERE cs_content ~ '\[(vi|en)\]\s'
    OR vi_content ~ '\[(vi|en)\]\s';
