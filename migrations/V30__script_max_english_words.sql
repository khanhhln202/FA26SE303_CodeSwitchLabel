INSERT INTO system_config (config_key, config_value, value_type, description)
VALUES ('script.max_english_words', '5', 'int', 'Maximum English words in each script pair')
ON CONFLICT (config_key) DO NOTHING;
