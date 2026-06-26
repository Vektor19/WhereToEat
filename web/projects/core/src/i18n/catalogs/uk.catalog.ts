/**
 * Ukrainian (`uk`) message catalog (Step 17) — the **launch default and only fully-populated**
 * catalog (design Goal 4 / Risk #7). It is the single source of every user-facing string in the app:
 * cross-cutting error copy (resolved from the error model's `messageKey`, Step 7), the smoothed-rating
 * notes (invariant #6), the geolocation failure messages (Step 15), and every component label that
 * earlier steps deferred to an i18n key.
 *
 * The catalog is a plain nested object keyed by dotted keys (ngx-translate flattens the nesting), so
 * it is framework-light and mirrored 1:1 by the RN rewrite. **Interpolation** uses ngx-translate's
 * `{{ name }}` syntax. Adding a locale is a sibling catalog file plus a {@link SUPPORTED_LOCALES}
 * entry — never a code change here.
 */
import type { TranslationCatalog } from '../translation-catalog';

export const UK_CATALOG: TranslationCatalog = {
  app: {
    brand: 'ДеПоїсти',
    skipToContent: 'Перейти до основного вмісту',
    languageLabel: 'Мова',
    chooseLanguage: 'Обрати мову',
  },
  footer: {
    dataAccuracyNotice:
      'Ціни (≈) та дані можуть бути неточними — актуальну інформацію уточнюйте безпосередньо в закладі.',
  },
  common: {
    loading: 'Завантаження…',
    nothingFound: 'Нічого не знайдено.',
    retry: 'Спробувати ще раз',
    back: 'Назад',
    // Intentionally empty — a neutral key for "resolve to nothing" placeholders (e.g. an absent
    // geolocation error), so a reactive translate() never renders a raw key when there is no message.
    empty: '',
  },
  errors: {
    validation: 'Перевірте введені дані та спробуйте ще раз.',
    unauthorized: 'Потрібен вхід у систему.',
    forbidden: 'Недостатньо прав для цієї дії.',
    notFound: 'Не знайдено.',
    server: 'Сталася помилка на сервері. Спробуйте пізніше.',
    network: "Немає з'єднання. Перевірте мережу.",
    unknown: 'Сталася невідома помилка.',
  },
  rating: {
    lowReviewNote: 'Оцінка за невеликою кількістю відгуків ({{ count }}).',
    noReviews: 'Поки немає відгуків.',
  },
  portal: {
    brand: 'ДеПоїсти · Кабінет',
    skipToContent: 'Перейти до основного вмісту',
    navLabel: 'Розділи кабінету',
    signIn: 'Увійти',
    signOut: 'Вийти',
    nav: {
      dashboard: 'Огляд',
      menu: 'Меню',
      protection: 'Захист даних',
      address: 'Адреса',
      photos: 'Фото',
      verified: 'Verified',
      ads: 'Реклама',
      analytics: 'Аналітика',
    },
    home: {
      title: 'Кабінет оператора',
      intro: 'Оберіть розділ ліворуч, щоб керувати даними закладів.',
    },
    denied: {
      title: 'Доступ заборонено',
      body: 'Цей розділ доступний лише операторам та адміністраторам.',
    },
    save: {
      saving: 'Збереження…',
      success: 'Збережено.',
    },
    picker: {
      restaurantLabel: 'ID закладу',
      hint: 'Введіть ідентифікатор закладу та натисніть «Завантажити».',
      load: 'Завантажити',
    },
    menu: {
      title: 'Керування меню',
      intro:
        'Редагуйте ціну, вагу та категорію позицій меню. Ручні зміни мають пріоритет над парсером.',
      restaurantLabel: 'ID закладу',
      itemsLabel: 'Позиції меню',
      noItems: 'У цього закладу поки немає позицій меню.',
      editTitle: 'Редагувати позицію',
      category: 'Категорія',
      dish: 'Страва',
      dishesLoading: 'Завантаження страв…',
      price: 'Ціна',
      currency: 'Валюта',
      weight: 'Вага / кількість',
      weightHint: 'Необов’язково, напр. «300 г».',
      save: 'Зберегти позицію',
    },
    protection: {
      title: 'Захист даних від парсера',
      intro:
        'Прапорці захищають вивірені вручну дані: парсер не створює/не оновлює позначені позиції та не перезаписує позначені заклади.',
      restaurantLabel: 'ID закладу',
      doNotUpdate: 'Не оновлювати заклад парсером',
      doNotUpdateHint: 'Парсер не перезаписуватиме дані цього закладу — вони вивірені вручну.',
      itemsTitle: 'Позиції меню',
      noItems: 'У цього закладу поки немає позицій меню.',
    },
    address: {
      title: 'Адреса закладу',
      intro: 'Редагуйте адресу закладу. Збереження запускає повторне геокодування координат.',
      restaurantLabel: 'ID закладу',
      line: 'Адреса (вулиця, будинок)',
      city: 'Місто',
      regeocodeNote: 'Після збереження координати буде перераховано автоматично (OSM).',
      save: 'Зберегти адресу',
    },
    photos: {
      title: 'Дозвіл на реальні фото',
      intro:
        'За замовчуванням на всіх стравах — наші генерик-фото. Реальні фото показуємо лише з дозволу закладу.',
      photoId: 'ID фото',
      photoIdHint: 'Введіть ідентифікатор фото.',
      permission: 'Дозволити показ реального фото',
      permissionHint:
        'Увімкнено — показуємо реальне фото; вимкнено — лишається генерик за замовчуванням.',
      save: 'Зберегти дозвіл',
    },
    verified: {
      title: 'Статус Verified',
      intro: 'Надайте закладу тариф Verified (Basic / Pro) або відкличте статус.',
      partnerNote:
        'Verified — це позначка партнерського / підтвердженого статусу (заклад керує своєю карткою, може додати реальні фото та меню «від власника»). Це не «знак якості від нас» — органічний рейтинг від статусу не залежить.',
      venueId: 'ID закладу',
      venueIdHint: 'Введіть ідентифікатор закладу.',
      tier: 'Тариф',
      tierBasic: 'Basic',
      tierPro: 'Pro',
      tierHint: 'Тариф для надання статусу Verified.',
      grant: 'Надати Verified',
      revoke: 'Відкликати Verified',
    },
    ads: {
      title: 'Рекламне розміщення',
      intro: 'Створіть таргетований рекламний слот із періодом показу для закладу.',
      labeledNote:
        'Це платний рекламний слот — він завжди позначається «Реклама» та візуально відокремлений від органічної видачі. Органічний рейтинг не продається; розміщення не впливає на органічне сортування.',
      venueId: 'ID закладу',
      targetingKey: 'Ключ таргетингу',
      targetingKeyHint: 'Напр., категорія страви або гео-район для показу.',
      startsAt: 'Початок показу',
      endsAt: 'Кінець показу',
      rangeError: 'Початок показу має бути раніше за кінець.',
      create: 'Створити розміщення',
      created: 'Розміщення створено. ID:',
    },
    analytics: {
      title: 'Аналітика закладу',
      intro:
        'Перегляньте знеособлену агреговану аналітику закладу за обраний період: видимість, попит, ціни, рейтинг та воронку конверсії.',
      privacyNote:
        'Усі дані — лише агреговані та знеособлені. Жодних персональних даних чи окремих дій користувачів тут немає й бути не може.',
      restaurantLabel: 'ID закладу',
      restaurantHint: 'Введіть ідентифікатор закладу.',
      from: 'Початок періоду',
      to: 'Кінець періоду',
      load: 'Показати аналітику',
      preLoad: 'Оберіть заклад і період, потім натисніть «Показати аналітику».',
      traffic: {
        title: 'Видимість і трафік',
        impressions: 'Покази у видачі',
        cardOpens: 'Відкриття картки',
        actions: 'Дії (карта / телефон / сайт)',
        ctr: 'CTR (показ → відкриття)',
      },
      funnel: {
        title: 'Воронка конверсії',
        impressions: 'Покази',
        cardOpens: 'Відкриття картки',
        actions: 'Дії',
        openRate: 'Показ → відкриття',
        actionRate: 'Відкриття → дія',
      },
      demand: {
        title: 'Попит за категоріями та стравами',
        note: 'Що шукають у вашому районі — навіть позиції, яких у вас немає.',
        categories: 'Категорії',
        dishes: 'Страви',
        selection: 'Ідентифікатор',
        searches: 'Пошуків',
        empty: 'За цей період пошуків не зафіксовано.',
      },
      price: {
        title: 'Цінове позиціонування',
        note: 'Ваші ціни відносно медіани по категорії/району.',
        dish: 'Страва',
        venuePrice: 'Ваша ціна',
        median: 'Медіана ринку',
        delta: 'Різниця',
        cheaper: 'Дешевше за ринок',
        dearer: 'Дорожче за ринок',
        equal: 'На рівні ринку',
        noMedian: 'Медіана недоступна',
        empty: 'Немає даних про ціни для цього закладу.',
      },
      ratings: {
        title: 'Рейтинг (за весь час)',
        average: 'Середня оцінка',
        count: 'Кількість оцінок',
        empty: 'Для цього закладу поки немає оцінок.',
      },
    },
  },
  consumer: {
    selection: {
      title: 'Ваш вибір',
      regionLabel: 'Обрані категорії та страви',
      clearAll: 'Очистити все',
      emptyHint: 'Додайте категорії або страви, щоб почати пошук.',
      removeAria: 'Прибрати {{ label }} з вибору',
    },
    browse: {
      tabLabel: 'Огляд категорій',
      searchTabLabel: 'Пошук',
      categories: 'Категорії',
      dishesOfCategory: 'Страви категорії',
      addWholeCategory: 'Додати всю категорію',
      categoryAdded: 'Категорію додано',
      categoriesLoading: 'Завантаження категорій…',
      dishesLoading: 'Завантаження страв…',
      categoriesEmpty: 'Категорій поки немає.',
      dishesEmpty: 'У цій категорії поки немає страв.',
      openCategoryAria: 'Відкрити категорію {{ name }}',
      addCategoryAria: 'Додати категорію {{ name }} до вибору',
      addDishAria: 'Додати страву {{ name }} до вибору',
    },
    search: {
      label: 'Пошук категорії або страви',
      placeholder: 'Почніть вводити назву…',
      clear: 'Очистити пошук',
      results: 'Результати пошуку',
      loading: 'Пошук…',
      empty: 'За цим запитом нічого не знайдено.',
    },
    match: {
      legend: 'Як поєднувати обране',
      or: 'Будь-яке (Або)',
      and: 'Комбо (І)',
      hintOr: 'Будь-яке: заклад має містити хоча б одну з обраних позицій.',
      hintAnd: 'Комбо: заклад має містити всі обрані позиції одночасно.',
    },
    sort: {
      legend: 'Сортувати за',
      price: 'Ціною',
      distance: 'Відстанню',
      rating: 'Рейтингом',
      priceQuality: 'Ціна-якість',
      best: 'Найкраще',
      hintExplicit:
        'Список упорядковано за полем «{{ field }}». Покриття («N з M») — лише додатковий критерій за рівних значень.',
      hintComposite:
        'Складений режим: заклади ранжуються за збалансованим показником (ціна, якість, відстань, покриття).',
    },
    filters: {
      legend: 'Фільтри',
      maxPrice: 'Максимальна ціна',
      maxPriceHint: 'Не дорожче, ніж (₴)',
      minRating: 'Мінімальний рейтинг',
      minRatingHint: 'Не нижче, ніж (0–5)',
      clearAria: 'Прибрати фільтр «{{ label }}»',
    },
    query: {
      submit: 'Знайти заклади',
    },
    results: {
      loading: 'Шукаємо заклади…',
      empty: 'За вашим запитом нічого не знайдено.',
      regionLabel: 'Результати пошуку',
      coverage: 'Збіги: {{ covered }} з {{ total }}',
      // Consolidated screen-reader label for the rating block (the star icon is decorative /
      // aria-hidden, so the value + count are otherwise announced as disjoint fragments).
      ratingAria: 'Рейтинг {{ value }} із 5 за {{ count }} відгуками',
      noRatingAria: 'Рейтингу ще немає',
      open: 'Детальніше',
      openAria: '{{ action }}: {{ name }}',
      viewMap: 'Глянути на карті',
      viewMapAria: '{{ action }}: {{ name }}',
      adLabel: 'Реклама',
    },
    map: {
      titlePrefix: 'На карті:',
      loading: 'Завантажуємо карту…',
      close: 'Закрити карту',
      noData: 'Для цього закладу немає даних карти.',
      deepLinkMessage: 'Відкрийте розташування закладу в Google Maps.',
      deepLinkLabel: 'Відкрити в Google Maps',
    },
    details: {
      loading: 'Завантажуємо заклад…',
      empty: 'Заклад не знайдено.',
      contactsTitle: 'Контакти',
      contactsRegion: 'Контактні посилання закладу',
      contactsEmpty: 'Контактні посилання поки не вказані.',
      menuTitle: 'Меню',
      menuRegion: 'Меню закладу',
      menuEmpty: 'Меню поки не додано.',
      dishFallback: 'Страва',
    },
    geo: {
      toggle: 'Враховувати відстань до закладу',
      acquiring: 'Визначаємо приблизне місцезнаходження…',
      consent:
        'Місцезнаходження приблизне й необов’язкове — лише щоб показати відстань і ближчі заклади. ' +
        'Ми його не продаємо. Можна вимкнути будь-коли, щоб шукати ширше за ціною чи якістю.',
      error: {
        permissionDenied: 'Доступ до місцезнаходження відхилено. Відстань не враховуватимемо.',
        positionUnavailable: 'Не вдалося визначити місцезнаходження. Спробуйте пізніше.',
        timeout: 'Визначення місцезнаходження зайняло забагато часу.',
        unsupported: 'Цей пристрій не підтримує визначення місцезнаходження.',
      },
    },
    rating: {
      // The rating-submit control (Step 22). Honest about invariant #6: the shown rating is our
      // smoothed/cumulative all-time value (not the user's raw last score), updated on recompute.
      title: 'Оцінити заклад',
      region: 'Оцінювання закладу',
      // Accessible name for each star button (radio); `{{ score }}` is the 1..5 value.
      star: '{{ score }} з 5',
      submit: 'Поставити оцінку',
      submitting: 'Зберігаємо оцінку…',
      success: 'Дякуємо! Вашу оцінку враховано.',
      smoothedNote:
        'Показуємо наш усереднений рейтинг за весь час — вашу оцінку буде враховано в ньому ' +
        'після перерахунку, а не як ваш окремий бал.',
      signInPrompt: 'Увійдіть, щоб оцінити заклад.',
      signIn: 'Увійти',
      chooseScore: 'Оберіть оцінку від 1 до 5.',
    },
  },
};
