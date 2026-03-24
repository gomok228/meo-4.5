using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Charactercontrol : MonoBehaviour
{
    Animator animator;
    BoxCollider2D box2d;
    Rigidbody2D rb2d;
    SpriteRenderer sprite;

    ColorSwap colorSwap;
    Collider2D currentPlatform;

    float keyHorizontal;
    float keyVertical;
    bool keyJump;
    bool keyShoot;

    bool isGrounded;
    bool isJumping;
    bool isShooting;
    bool isThrowing = false;          // для совместимости с HyperBomb
    bool isTeleporting;
    bool isTakingDamage;
    bool isInvincible;
    bool isFacingRight;

    // --- переменные лестницы (из PlayerController) ---
    bool isClimbing;
    bool isClimbingDown;
    bool atLaddersEnd;
    bool hasStartedClimbing;
    bool startedClimbTransition;
    bool finishedClimbTransition;
    float transformY;
    float transformHY;
    // ------------------------------------------------

    bool hitSideRight;
    bool freezeInput;
    bool freezePlayer;
    bool freezeBullets;

    float shootTime;
    bool keyShootRelease;
    float keyShootReleaseTimeLength;

    RigidbodyConstraints2D rb2dConstraints;

    string lastAnimationName;

    // Кэширование слоев для оптимизации (чтобы не искать их каждый кадр)
    int groundLayerMask;
    int teleportLayer;
    int playerLayer;

    // ---- Новые поля из PlayerController ----
    public enum WeaponTypes
    {
        HyperBomb,
        ThunderBeam,
        SuperArm,
        IceSlasher,
        RollingCutter,
        FireStorm,
        MagnetBeam,
        MegaBuster
    }

    public WeaponTypes playerWeapon = WeaponTypes.MegaBuster;

    [System.Serializable]
    public struct WeaponsStruct
    {
        public WeaponTypes weaponType;
        public bool enabled;
        public int currentEnergy;
        public int maxEnergy;
        public int energyCost;
        public int weaponDamage;
        public Vector2 weaponVelocity;
        public AudioClip weaponClip;
        public GameObject weaponPrefab;
    }
    public WeaponsStruct[] weaponsData;

    // Флаги для стрельбы
    private bool canUseWeapon = true;

    // Для зарядки MegaBuster (ИСПРАВЛЕНО)
    private bool isCharging = false;
    private float chargeTime = 0f;
    private bool chargeShotReady = false;

    // Для деша (ИСПРАВЛЕНО под Mega Man Zero)
    [Header("Dash Settings (Zero Style)")]
    public float dashSpeed = 6.5f;
    public float dashDuration = 0.35f;
    public float dashCooldown = 0.5f;
    private bool canDash = true;
    private bool isDashing = false;
    private float dashTimeLeft = 0f;

    // ---- Оставшиеся старые поля ----
    private enum SwapIndex
    {
        Primary = 64,
        Secondary = 128
    }

    public enum PlayerWeapons { Default, MagnetBeam, BombMan, CutMan, ElecMan, FireMan, GutsMan, IceMan };
    public PlayerWeapons playerWeaponLegacy = PlayerWeapons.Default;

    [System.Serializable]
    public struct PlayerWeaponsStruct
    {
        public PlayerWeapons weapon;
        public bool enabled;
        public int currentEnergy;
        public int maxEnergy;
        public int energyCost;
        public int weaponDamage;
        public GameObject weaponPrefab;
    }
    public PlayerWeaponsStruct[] playerWeaponStructs;

    public int currentHealth;
    public int maxHealth = 28;

    [SerializeField] float moveSpeed = 1.5f;
    [SerializeField] float jumpSpeed = 3.7f;

    [SerializeField] float climbSpeed = 0.525f;
    [SerializeField] float climbSpriteHeight = 0.36f;

    [SerializeField] int bulletDamage = 1;
    [SerializeField] float bulletSpeed = 5f;

    [Header("Audio Clips")]
    [SerializeField] AudioClip teleportClip;
    [SerializeField] AudioClip jumpLandedClip;
    [SerializeField] AudioClip shootBulletClip;
    [SerializeField] AudioClip takingDamageClip;
    [SerializeField] AudioClip explodeEffectClip;
    [SerializeField] AudioClip energyFillClip;

    [Header("Positions and Prefabs")]
    [SerializeField] Transform bulletShootPos;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] GameObject explodeEffectPrefab;

    [Header("Teleport Settings")]
    [SerializeField] float teleportSpeed = -10f;
    [SerializeField] float teleportLandingY = 0f;
    public enum TeleportState { Descending, Landed, Idle };
    [SerializeField] TeleportState teleportState;

    [HideInInspector] public LadderScript ladder;

    void Awake()
    {
        animator = GetComponent<Animator>();
        box2d = GetComponent<BoxCollider2D>();
        rb2d = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();

        // Оптимизация: кэшируем слои один раз при запуске
        groundLayerMask = 1 << LayerMask.NameToLayer("Ground");
        teleportLayer = LayerMask.NameToLayer("Teleport");
        playerLayer = LayerMask.NameToLayer("Player");
    }

    void Start()
    {
        isFacingRight = true;
        currentHealth = maxHealth;
        colorSwap = GetComponent<ColorSwap>();
        SetWeaponLegacy(playerWeaponLegacy);
        FillWeaponEnergies();

        // Инициализация нового массива оружия, если не задан
        if (weaponsData == null || weaponsData.Length == 0)
        {
            InitWeaponsData();
        }
        SetWeapon(playerWeapon);

#if UNITY_STANDALONE
        GameObject inputCanvas = GameObject.Find("InputCanvas");
        if (inputCanvas != null) inputCanvas.SetActive(false);
#endif
    }

    void InitWeaponsData()
    {
        int num = Enum.GetValues(typeof(WeaponTypes)).Length;
        weaponsData = new WeaponsStruct[num];
        for (int i = 0; i < num; i++)
        {
            weaponsData[i] = new WeaponsStruct
            {
                weaponType = (WeaponTypes)i,
                enabled = (i == (int)WeaponTypes.MegaBuster),
                currentEnergy = 100,
                maxEnergy = 100,
                energyCost = 10,
                weaponDamage = 1,
                weaponVelocity = new Vector2(5f, 0),
                weaponClip = null,
                weaponPrefab = null
            };
        }
    }

    void FixedUpdate()
    {
        isGrounded = false;
        RaycastHit2D raycastHit;
        float raycastDistance = 0.05f;

        Vector3 box_origin = box2d.bounds.center;
        box_origin.y = box2d.bounds.min.y + (box2d.bounds.extents.y / 4f);
        Vector3 box_size = box2d.bounds.size;
        box_size.y = box2d.bounds.size.y / 4f;
        
        // Используем закэшированный слой для производительности
        raycastHit = Physics2D.BoxCast(box_origin, box_size, 0f, Vector2.down, raycastDistance, groundLayerMask);

        if (raycastHit.collider != null && gameObject.layer != teleportLayer)
        {
            isGrounded = true;
            if (isJumping)
            {
                isJumping = false;
                SoundManager.Instance.Play(jumpLandedClip);
            }
        }
        
        Color raycastColor = (isGrounded) ? Color.green : Color.red;
        Debug.DrawRay(box_origin + new Vector3(box2d.bounds.extents.x, 0), Vector2.down * (box2d.bounds.extents.y / 4f + raycastDistance), raycastColor);
        Debug.DrawRay(box_origin - new Vector3(box2d.bounds.extents.x, 0), Vector2.down * (box2d.bounds.extents.y / 4f + raycastDistance), raycastColor);
        Debug.DrawRay(box_origin - new Vector3(box2d.bounds.extents.x, box2d.bounds.extents.y / 4f + raycastDistance), Vector2.right * (box2d.bounds.extents.x * 2), raycastColor);
    }

    void Update()
    {
        // Телепорт
        if (isTeleporting)
        {
            switch (teleportState)
            {
                case TeleportState.Descending:
                    isJumping = false;
                    if (isGrounded || transform.position.y <= teleportLandingY)
                    {
                        gameObject.tag = "Player";
                        gameObject.layer = playerLayer;
                        rb2d.linearVelocity = Vector2.zero;
                        rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
                        teleportState = TeleportState.Landed;
                    }
                    break;
                case TeleportState.Landed:
                    animator.speed = 1;
                    break;
                case TeleportState.Idle:
                    Teleport(false);
                    break;
            }
            return;
        }

        if (isTakingDamage)
        {
            animator.Play("hit");
            return;
        }

        PlayerDebugInput();
        PlayerDirectionInput();
        PlayerJumpInput();

        // Проверка старта дэша
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isClimbing && !isTakingDamage && isGrounded && !isDashing)
        {
            StartDash();
        }

        // Обработка стрельбы
        PlayerShootInput();
        FireWeapon(); 

        // Движение (основная логика перемещения и дэша)
        PlayerMovement();
    }

    // ======================== ОСНОВНЫЕ МЕТОДЫ ИЗ PLAYERCONTROLLER ========================

    public void SetWeapon(WeaponTypes weapon)
    {
        playerWeapon = weapon;
        Debug.Log($"Weapon changed to {weapon}");
    }

    public void SwitchWeapon(WeaponTypes weaponType)
    {
        int idx = (int)weaponType;
        if (idx >= 0 && idx < weaponsData.Length && weaponsData[idx].enabled)
        {
            SetWeapon(weaponType);
            Teleport(true);
            StartCoroutine(WeaponSwitchDelay());
        }
    }

    IEnumerator WeaponSwitchDelay()
    {
        canUseWeapon = false;
        yield return new WaitForSeconds(0.3f);
        canUseWeapon = true;
    }

    void FireWeapon()
    {
        switch (playerWeapon)
        {
            case WeaponTypes.MegaBuster:
                MegaBuster();
                break;
            case WeaponTypes.HyperBomb:
                HyperBomb();
                break;
            default:
                MegaBuster();
                break;
        }
    }

    // ИСПРАВЛЕННЫЙ Чардж Шот
    void MegaBuster()
    {
        if (!canUseWeapon || freezeInput) return;

        // 1. Момент нажатия кнопки: стреляем обычной пулей и начинаем зарядку
        if (keyShoot && keyShootRelease)
        {
            ShootBullet();
            isCharging = true;
            chargeTime = 0f;
            chargeShotReady = false;
        }

        // 2. Удержание кнопки: копим время
        if (keyShoot && isCharging)
        {
            chargeTime += Time.deltaTime;
            if (chargeTime >= 1f && !chargeShotReady)
            {
                chargeShotReady = true;
                Debug.Log("Mega Buster fully charged!");
                // Здесь можно запустить звук или анимацию полной зарядки
            }
        }

        // 3. Отпускание кнопки: выпускаем чардж-шот, если успели накопить
        if (!keyShoot && isCharging)
        {
            isCharging = false;
            if (chargeShotReady)
            {
                ShootChargedBullet();
            }
            chargeShotReady = false;
            chargeTime = 0f;
        }
    }

    void ShootChargedBullet()
    {
        int idx = (int)WeaponTypes.MegaBuster;
        if (weaponsData[idx].weaponPrefab == null)
        {
            ShootBullet(); // fallback
            return;
        }
        GameObject bullet = Instantiate(weaponsData[idx].weaponPrefab, bulletShootPos.position, Quaternion.identity);
        bullet.name = "ChargedBullet";
        var bulletScript = bullet.GetComponent<bulletControll>();
        if (bulletScript != null)
        {
            bulletScript.SetDamageValue(weaponsData[idx].weaponDamage * 3);
            bulletScript.SetBulletSpeed(weaponsData[idx].weaponVelocity.x * 1.5f);
            bulletScript.SetBulletDirection((isFacingRight) ? Vector2.right : Vector2.left);
            bulletScript.SetDestroyDelay(5f);
            bulletScript.Shoot();
        }
        if (weaponsData[idx].weaponClip != null)
        {
            SoundManager.Instance.Play(weaponsData[idx].weaponClip);
        }
    }

    void HyperBomb()
    {
        if (keyShoot && keyShootRelease && canUseWeapon)
        {
            int idx = (int)WeaponTypes.HyperBomb;
            if (idx >= 0 && idx < weaponsData.Length && weaponsData[idx].enabled && weaponsData[idx].currentEnergy >= weaponsData[idx].energyCost)
            {
                isThrowing = true;
                canUseWeapon = false;
                keyShootRelease = false;
                shootTime = Time.time;
                StartCoroutine(ThrowBombRoutine());
                SpendWeaponEnergy(WeaponTypes.HyperBomb);
                RefreshWeaponEnergyBar(WeaponTypes.HyperBomb);
            }
        }
        if (isThrowing && Time.time - shootTime >= 0.25f)
        {
            isThrowing = false;
        }
    }

    // ИСПРАВЛЕНИЕ УЯЗВИМОСТИ: Заменил Invoke на Coroutine, это безопаснее
    IEnumerator ThrowBombRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        ThrowBomb();
    }

    void ThrowBomb()
    {
        int idx = (int)WeaponTypes.HyperBomb;
        if (weaponsData[idx].weaponPrefab == null) return;
        GameObject bomb = Instantiate(weaponsData[idx].weaponPrefab, bulletShootPos.position, Quaternion.identity);
        bomb.name = weaponsData[idx].weaponPrefab.name;
        var bombScript = bomb.GetComponent<BombScript>();
        if (bombScript != null)
        {
            bombScript.SetContactDamageValue(0);
            bombScript.SetExplosionDamageValue(weaponsData[idx].weaponDamage);
            bombScript.SetExplosionDelay(3f);
            bombScript.SetCollideWithTags("Enemy");
            bombScript.SetDirection((isFacingRight) ? Vector2.right : Vector2.left);
            bombScript.SetVelocity(weaponsData[idx].weaponVelocity);
            bombScript.Bounces(true);
            bombScript.ExplosionEvent.AddListener(() => canUseWeapon = true);
            bombScript.Launch(false);
        }
        if (weaponsData[idx].weaponClip != null)
        {
            SoundManager.Instance.Play(weaponsData[idx].weaponClip);
        }
    }

    void SpendWeaponEnergy(WeaponTypes weaponType)
    {
        int idx = (int)weaponType;
        weaponsData[idx].currentEnergy -= weaponsData[idx].energyCost;
        weaponsData[idx].currentEnergy = Mathf.Clamp(weaponsData[idx].currentEnergy, 0, weaponsData[idx].maxEnergy);
    }

    void RefreshWeaponEnergyBar(WeaponTypes weaponType)
    {
        int idx = (int)weaponType;
        if (UIEnergyBars.Instance != null)
        {
            UIEnergyBars.Instance.SetValue(
                UIEnergyBars.EnergyBars.PlayerWeapon,
                weaponsData[idx].currentEnergy / (float)weaponsData[idx].maxEnergy);
        }
    }

    public void EnableWeaponPart(ItemScript.WeaponPartEnemies weaponPartEnemy)
    {
        switch (weaponPartEnemy)
        {
            case ItemScript.WeaponPartEnemies.BombMan:
                weaponsData[(int)WeaponTypes.HyperBomb].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.CutMan:
                weaponsData[(int)WeaponTypes.RollingCutter].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.ElecMan:
                weaponsData[(int)WeaponTypes.ThunderBeam].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.FireMan:
                weaponsData[(int)WeaponTypes.FireStorm].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.GutsMan:
                weaponsData[(int)WeaponTypes.SuperArm].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.IceMan:
                weaponsData[(int)WeaponTypes.IceSlasher].enabled = true;
                break;
        }
        ApplyWeaponPartLegacy(weaponPartEnemy);
    }

    void ApplyWeaponPartLegacy(ItemScript.WeaponPartEnemies weaponPartEnemy)
    {
        switch (weaponPartEnemy)
        {
            case ItemScript.WeaponPartEnemies.BombMan:
                playerWeaponStructs[(int)PlayerWeapons.BombMan].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.CutMan:
                playerWeaponStructs[(int)PlayerWeapons.CutMan].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.ElecMan:
                playerWeaponStructs[(int)PlayerWeapons.ElecMan].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.FireMan:
                playerWeaponStructs[(int)PlayerWeapons.FireMan].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.GutsMan:
                playerWeaponStructs[(int)PlayerWeapons.GutsMan].enabled = true;
                break;
            case ItemScript.WeaponPartEnemies.IceMan:
                playerWeaponStructs[(int)PlayerWeapons.IceMan].enabled = true;
                break;
        }
    }

    void ShootBullet()
    {
        int idx = (int)playerWeapon;
        if (weaponsData[idx].weaponPrefab == null)
        {
            GameObject bullet = Instantiate(bulletPrefab, bulletShootPos.position, Quaternion.identity);
            bullet.name = bulletPrefab.name;
            var bulletScript = bullet.GetComponent<bulletControll>();
            if (bulletScript != null)
            {
                bulletScript.SetDamageValue(bulletDamage);
                bulletScript.SetBulletSpeed(bulletSpeed);
                bulletScript.SetBulletDirection((isFacingRight) ? Vector2.right : Vector2.left);
                bulletScript.SetDestroyDelay(5f);
                bulletScript.Shoot();
            }
            if (shootBulletClip != null) SoundManager.Instance.Play(shootBulletClip);
            return;
        }

        GameObject newBullet = Instantiate(weaponsData[idx].weaponPrefab, bulletShootPos.position, Quaternion.identity);
        newBullet.name = weaponsData[idx].weaponPrefab.name;
        var script = newBullet.GetComponent<bulletControll>();
        if (script != null)
        {
            script.SetDamageValue(weaponsData[idx].weaponDamage);
            script.SetBulletSpeed(weaponsData[idx].weaponVelocity.x);
            script.SetBulletDirection((isFacingRight) ? Vector2.right : Vector2.left);
            script.SetDestroyDelay(5f);
            script.Shoot();
        }
        if (weaponsData[idx].weaponClip != null) SoundManager.Instance.Play(weaponsData[idx].weaponClip);
    }

    // ======================== ДВИЖЕНИЕ И ДЭШ ========================

    void StartDash()
    {
        isDashing = true;
        dashTimeLeft = dashDuration;
        canDash = false;
    }

    void StopDash()
    {
        isDashing = false;
        StartCoroutine(DashCooldownRoutine());
    }

    IEnumerator DashCooldownRoutine()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void PlayerMovement()
    {
        transformY = transform.position.y;
        transformHY = transformY + climbSpriteHeight;
        float speed = moveSpeed;

        if (isClimbing)
        {
            if (ladder == null) { ResetClimbing(); return; }

            Debug.DrawLine(new Vector3(ladder.posX - 2f, ladder.posTopHandlerY, 0),
                new Vector3(ladder.posX + 2f, ladder.posTopHandlerY, 0), Color.blue);
            Debug.DrawLine(new Vector3(ladder.posX - 2f, ladder.posBottomHandlerY, 0),
                new Vector3(ladder.posX + 2f, ladder.posBottomHandlerY, 0), Color.blue);
            Debug.DrawLine(new Vector3(transform.position.x - 2f, transformHY, 0),
                new Vector3(transform.position.x + 2f, transformHY, 0), Color.magenta);
            Debug.DrawLine(new Vector3(transform.position.x - 2f, transformY, 0),
                new Vector3(transform.position.x + 2f, transformY, 0), Color.magenta);

            if (transformHY > ladder.posTopHandlerY)
            {
                if (!isClimbingDown)
                {
                    if (!startedClimbTransition)
                    {
                        startedClimbTransition = true;
                        ClimbTransition(true);
                    }
                    else if (finishedClimbTransition)
                    {
                        finishedClimbTransition = false;
                        isJumping = false;
                        animator.Play("idle");
                        transform.position = new Vector2(ladder.posX, ladder.posPlatformY + 0.005f);
                        if (!atLaddersEnd)
                        {
                            atLaddersEnd = true;
                            Invoke("ResetClimbing", 0.1f);
                        }
                    }
                }
            }
            else if (transformHY < ladder.posBottomHandlerY)
            {
                ResetClimbing();
            }
            else
            {
                if (!isClimbingDown)
                {
                    if (keyJump && keyVertical == 0)
                        ResetClimbing();
                    else if (isGrounded && !hasStartedClimbing)
                    {
                        isJumping = false;
                        animator.Play("idle");
                        transform.position = new Vector2(ladder.posX, ladder.posBottomY - 0.005f);
                        if (!atLaddersEnd)
                        {
                            atLaddersEnd = true;
                            Invoke("ResetClimbing", 0.1f);
                        }
                    }
                    else
                    {
                        animator.speed = Mathf.Abs(keyVertical);
                        if (keyVertical != 0 && !isShooting && !isThrowing)
                        {
                            Vector3 climbDirection = new Vector3(0, climbSpeed) * keyVertical;
                            transform.position = transform.position + climbDirection * Time.deltaTime;
                        }
                        if (isShooting || isThrowing)
                        {
                            if (keyHorizontal < 0 && isFacingRight) Flip();
                            else if (keyHorizontal > 0 && !isFacingRight) Flip();
                            if (isShooting) animator.Play("ladder_shoot");
                        }
                        else
                        {
                            animator.Play("ladder_climb");
                        }
                    }
                }
            }
        }
        else
        {
            // ИСПРАВЛЕННЫЙ ДЭШ (Стиль Mega Man Zero)
            if (isDashing)
            {
                dashTimeLeft -= Time.deltaTime;
                rb2d.linearVelocity = new Vector2((isFacingRight ? 1 : -1) * dashSpeed, rb2d.linearVelocity.y);
                animator.Play("slide");

                // Прерываем дэш, если время вышло, или если игрок нажал в противоположную сторону
                if (dashTimeLeft <= 0 || (isFacingRight && keyHorizontal < 0) || (!isFacingRight && keyHorizontal > 0))
                {
                    StopDash();
                }

                // Дэш-прыжок (сохраняем скорость)
                if (keyJump && isGrounded)
                {
                    StopDash();
                    rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpSpeed);
                    StartCoroutine(JumpCo());
                }
            }
            else
            {
                // Обычное передвижение
                if (keyHorizontal < 0)
                {
                    if (isFacingRight) Flip();
                    if (isGrounded)
                    {
                        if (isShooting) animator.Play("shoot_run");
                        else animator.Play("Run");
                    }
                }
                else if (keyHorizontal > 0)
                {
                    if (!isFacingRight) Flip();
                    if (isGrounded)
                    {
                        if (isShooting) animator.Play("shoot_run");
                        else animator.Play("Run");
                    }
                }
                else
                {
                    if (isGrounded)
                    {
                        if (isShooting) animator.Play("shoot");
                        else animator.Play("idle");
                    }
                }
                
                rb2d.linearVelocity = new Vector2(speed * keyHorizontal, rb2d.linearVelocity.y);

                if (keyJump && isGrounded)
                {
                    rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpSpeed);
                    StartCoroutine(JumpCo());
                }

                if (!isGrounded)
                {
                    isJumping = true;
                    if (isShooting) animator.Play("jump");
                }
            }

            StartClimbingUp();
            StartClimbingDown();
        }
    }

    IEnumerator JumpCo()
    {
        yield return new WaitForSeconds(Time.fixedDeltaTime);
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        transform.Rotate(0f, 180f, 0f);
    }

    // Лестничные методы
    void StartedClimbing() => StartCoroutine(StartedClimbingCo());
    IEnumerator StartedClimbingCo() { hasStartedClimbing = true; yield return new WaitForSeconds(0.1f); hasStartedClimbing = false; }

    public void StartClimbingUp()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical > 0 && transformHY < ladder.posTopHandlerY)
        {
            if (isDashing) StopDash(); // Отменяем дэш при залезании
            isClimbing = true; isClimbingDown = false; animator.speed = 0;
            rb2d.bodyType = RigidbodyType2D.Kinematic; rb2d.linearVelocity = Vector2.zero;
            transform.position = new Vector3(ladder.posX, transformY + 0.025f, 0);
            StartedClimbing();
        }
    }

    public void StartClimbingDown()
    {
        if (ladder != null && ladder.isNearLadder && keyVertical < 0 && isGrounded && transformHY > ladder.posTopHandlerY)
        {
            if (isDashing) StopDash(); // Отменяем дэш при слезании
            isClimbing = true; isClimbingDown = true; animator.speed = 0;
            rb2d.bodyType = RigidbodyType2D.Kinematic; rb2d.linearVelocity = Vector2.zero;
            transform.position = new Vector3(ladder.posX, transformY, 0);
            ClimbTransition(false);
        }
    }

    public void ResetClimbing()
    {
        if (isClimbing)
        {
            isClimbing = false; atLaddersEnd = false; startedClimbTransition = false; finishedClimbTransition = false;
            animator.speed = 1; rb2d.bodyType = RigidbodyType2D.Dynamic; rb2d.linearVelocity = Vector2.zero;
        }
    }

    void ClimbTransition(bool movingUp) => StartCoroutine(ClimbTransitionCo(movingUp));
    IEnumerator ClimbTransitionCo(bool movingUp)
    {
        FreezeInput(true);
        finishedClimbTransition = false;
        Vector3 newPos = Vector3.zero;
        if (movingUp) newPos = new Vector3(ladder.posX, transformY + ladder.handlerTopOffset, 0);
        else
        {
            transform.position = new Vector3(ladder.posX, ladder.posTopHandlerY - climbSpriteHeight + ladder.handlerTopOffset, 0);
            newPos = new Vector3(ladder.posX, ladder.posTopHandlerY - climbSpriteHeight, 0);
        }
        while (transform.position != newPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, newPos, climbSpeed * Time.deltaTime);
            animator.speed = 1;
            animator.Play("Top");
            yield return null;
        }
        isClimbingDown = false;
        finishedClimbTransition = true;
        FreezeInput(false);
    }

    // Ввод с клавиатуры и отладка
    void PlayerDirectionInput()
    {
        if (!freezeInput)
        {
            keyHorizontal = Input.GetAxisRaw("Horizontal");
            keyVertical = Input.GetAxisRaw("Vertical");
        }
    }

    void PlayerJumpInput()
    {
        if (!freezeInput) keyJump = Input.GetKeyDown(KeyCode.Space);
    }

    void PlayerShootInput()
    {
        if (!freezeInput) keyShoot = Input.GetKey(KeyCode.C);

        if (keyShoot && keyShootRelease)
        {
            keyShootRelease = false;
            shootTime = Time.time;
        }
        if (!keyShoot && !keyShootRelease)
        {
            keyShootReleaseTimeLength = Time.time - shootTime;
            keyShootRelease = true;
        }
    }

    void PlayerDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            GameObject[] bullets = GameObject.FindGameObjectsWithTag("Bullet");
            freezeBullets = !freezeBullets;
            foreach (GameObject b in bullets) b.GetComponent<bulletControll>()?.FreezeBullet(freezeBullets);
            Debug.Log("Freeze Bullets: " + freezeBullets);
        }
        if (Input.GetKeyDown(KeyCode.E)) { Defeat(); Debug.Log("Defeat()"); }
        if (Input.GetKeyDown(KeyCode.I)) { Invincible(!isInvincible); Debug.Log("Invincible: " + isInvincible); }
        if (Input.GetKeyDown(KeyCode.L)) { ApplyLifeEnergy(10); Debug.Log("ApplyLifeEnergy(10)"); }
        if (Input.GetKeyDown(KeyCode.K)) { FreezeInput(!freezeInput); Debug.Log("Freeze Input: " + freezeInput); }
        if (Input.GetKeyDown(KeyCode.P)) { FreezePlayer(!freezePlayer); Debug.Log("Freeze Player: " + freezePlayer); }
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetWeapon((WeaponTypes)UnityEngine.Random.Range(0, Enum.GetValues(typeof(WeaponTypes)).Length));
            Teleport(true);
            Debug.Log("Teleport(true)");
        }
    }

    // Старые методы SetWeapon, ApplyLifeEnergy и др. (оставлены для обратной совместимости)
    public void SetWeaponLegacy(PlayerWeapons weapon)
    {
        playerWeaponLegacy = weapon;
        int currentEnergy = playerWeaponStructs[(int)playerWeaponLegacy].currentEnergy;
        int maxEnergy = playerWeaponStructs[(int)playerWeaponLegacy].maxEnergy;
        float weaponEnergyValue = (float)currentEnergy / (float)maxEnergy;

        switch (playerWeaponLegacy)
        {
            case PlayerWeapons.Default:
                colorSwap.SwapColor((int)SwapIndex.Primary, ColorSwap.ColorFromInt(0x0073F7));
                colorSwap.SwapColor((int)SwapIndex.Secondary, ColorSwap.ColorFromInt(0x00FFFF));
                if (UIEnergyBars.Instance != null)
                {
                    UIEnergyBars.Instance.SetImage(UIEnergyBars.EnergyBars.PlayerWeapon, UIEnergyBars.EnergyBarTypes.PlayerLife);
                    UIEnergyBars.Instance.SetVisibility(UIEnergyBars.EnergyBars.PlayerWeapon, false);
                }
                break;
            case PlayerWeapons.BombMan:
                colorSwap.SwapColor((int)SwapIndex.Primary, ColorSwap.ColorFromInt(0x009400));
                colorSwap.SwapColor((int)SwapIndex.Secondary, ColorSwap.ColorFromInt(0xFCFCFC));
                if (UIEnergyBars.Instance != null)
                {
                    UIEnergyBars.Instance.SetImage(UIEnergyBars.EnergyBars.PlayerWeapon, UIEnergyBars.EnergyBarTypes.HyperBomb);
                    UIEnergyBars.Instance.SetValue(UIEnergyBars.EnergyBars.PlayerWeapon, weaponEnergyValue);
                    UIEnergyBars.Instance.SetVisibility(UIEnergyBars.EnergyBars.PlayerWeapon, true);
                }
                break;
        }
        colorSwap.ApplyColor();
    }

    public void ApplyLifeEnergy(int amount)
    {
        if (currentHealth < maxHealth)
        {
            int healthDiff = maxHealth - currentHealth;
            if (healthDiff > amount) healthDiff = amount;
            StartCoroutine(AddLifeEnergy(healthDiff));
        }
    }

    IEnumerator AddLifeEnergy(int amount)
    {
        SoundManager.Instance.Play(energyFillClip, true);
        for (int i = 0; i < amount; i++)
        {
            currentHealth++;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            UIEnergyBars.Instance.SetValue(UIEnergyBars.EnergyBars.PlayerHealth, currentHealth / (float)maxHealth);
            yield return new WaitForSeconds(0.05f);
        }
        SoundManager.Instance.Stop();
    }
    public void ApplyWeaponEnergy(int amount)
    {
        int wt = (int)playerWeapon;
        if (weaponsData[wt].currentEnergy < weaponsData[wt].maxEnergy)
        {
            int energyDiff = weaponsData[wt].maxEnergy - weaponsData[wt].currentEnergy;
            if (energyDiff > amount) energyDiff = amount;
            StartCoroutine(AddWeaponEnergy(energyDiff));
        }
    }

    private IEnumerator AddWeaponEnergy(int amount)
    {
        int wt = (int)playerWeapon;
        SoundManager.Instance.Play(energyFillClip, true);
        for (int i = 0; i < amount; i++)
        {
            weaponsData[wt].currentEnergy++;
            weaponsData[wt].currentEnergy = Mathf.Clamp(weaponsData[wt].currentEnergy, 0, weaponsData[wt].maxEnergy);
            UIEnergyBars.Instance.SetValue(
                UIEnergyBars.EnergyBars.PlayerWeapon,
                weaponsData[wt].currentEnergy / (float)weaponsData[wt].maxEnergy);
            yield return new WaitForSeconds(0.05f);
        }
        SoundManager.Instance.Stop();
    }

    private IEnumerator AddWeaponEnergy(WeaponTypes weaponType,int amount)
    {
        int idx = (int)weaponType;
        SoundManager.Instance.Play(energyFillClip, true);
        for (int i = 0; i < amount; i++)
        {
            weaponsData[idx].currentEnergy++;
            weaponsData[idx].currentEnergy = Mathf.Clamp(weaponsData[idx].currentEnergy, 0, weaponsData[idx].maxEnergy);
            if (weaponType == playerWeapon && UIEnergyBars.Instance != null)
            {
                UIEnergyBars.Instance.SetValue(
                    UIEnergyBars.EnergyBars.PlayerWeapon,
                    weaponsData[idx].currentEnergy / (float)weaponsData[idx].maxEnergy);
            }
            yield return new WaitForSeconds(0.05f);
        }
        SoundManager.Instance.Stop();
    }

    public void FillWeaponEnergies()
    {
        for (int i = 0; i < playerWeaponStructs.Length; i++)
            playerWeaponStructs[i].currentEnergy = playerWeaponStructs[i].maxEnergy;
        for (int i = 0; i < weaponsData.Length; i++)
            weaponsData[i].currentEnergy = weaponsData[i].maxEnergy;
    }

    // Взаимодействие с уроном, смертью и т.д.
    public void HitSide(bool rightSide) => hitSideRight = rightSide;
    public void Invincible(bool invincibility) => isInvincible = invincibility;

    public void TakeDamage(int damage)
    {
        if (!isInvincible)
        {
            currentHealth -= damage;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            UIEnergyBars.Instance?.SetValue(UIEnergyBars.EnergyBars.PlayerHealth, currentHealth / (float)maxHealth);
            if (currentHealth <= 0) Defeat();
            else StartDamageAnimation();
        }
    }

    void StartDamageAnimation()
    {
        if (!isTakingDamage)
        {
            isTakingDamage = true;
            Invincible(true);
            FreezeInput(true);
            ResetClimbing();
            float hitForceX = 0.50f, hitForceY = 1.5f;
            if (hitSideRight) hitForceX = -hitForceX;
            rb2d.linearVelocity = Vector2.zero;
            rb2d.AddForce(new Vector2(hitForceX, hitForceY), ForceMode2D.Impulse);
            if (takingDamageClip != null) SoundManager.Instance.Play(takingDamageClip);
        }
    }

    void StopDamageAnimation()
    {
        isTakingDamage = false;
        FreezeInput(false);
        animator.Play("hit", -1, 0f);
        StartCoroutine(FlashAfterDamage());
    }

    IEnumerator FlashAfterDamage()
    {
        float flashDelay = 0.0833f;
        for (int i = 0; i < 10; i++)
        {
            sprite.material.SetFloat("_Transparency", 0f);
            yield return new WaitForSeconds(flashDelay);
            sprite.material.SetFloat("_Transparency", 1f);
            yield return new WaitForSeconds(flashDelay);
        }
        Invincible(false);
    }

    IEnumerator StartDefeatAnimation(bool explode)
    {
        yield return new WaitForSeconds(0.5f);
        FreezeInput(true);
        FreezePlayer(true);
        if (explode && explodeEffectPrefab != null)
        {
            GameObject explodeEffect = Instantiate(explodeEffectPrefab);
            explodeEffect.name = explodeEffectPrefab.name;
            explodeEffect.transform.position = sprite.bounds.center;
            explodeEffect.GetComponent<ExplosionScript>().SetDestroyDelay(5f);
        }
        if (explodeEffectClip != null) SoundManager.Instance.Play(explodeEffectClip);
        Destroy(gameObject);
    }

    public void Defeat(bool explode = true)
    {
        GameManager.Instance.PlayerDefeated();
        StartCoroutine(StartDefeatAnimation(explode));
    }

    public void FreezeInput(bool freeze)
    {
        freezeInput = freeze;
        if (freeze)
        {
            keyHorizontal = 0; keyVertical = 0; keyJump = false; keyShoot = false;
        }
    }

    public void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            freezePlayer = true;
            rb2dConstraints = rb2d.constraints;
            animator.speed = 0;
            rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            freezePlayer = false;
            animator.speed = 1;
            rb2d.constraints = rb2dConstraints;
        }
    }

    public void Teleport(bool teleport)
    {
        if (teleport)
        {
            isTeleporting = true;
            FreezeInput(true);
            animator.Play("Player_Teleport");
            teleportState = TeleportState.Descending;
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, teleportSpeed);
        }
        else
        {
            isTeleporting = false;
            FreezeInput(false);
        }
    }

    void TeleportAnimationSound() { if(teleportClip != null) SoundManager.Instance.Play(teleportClip); }
    void TeleportAnimationEnd() => teleportState = TeleportState.Idle;

    // Мобильные обёртки
    public void MobileShootWrapper()
    {
        if (!freezeInput) StartCoroutine(MobileShoot());
    }
    IEnumerator MobileShoot() { keyShoot = true; yield return new WaitForSeconds(0.01f); keyShoot = false; }

    public void MobileJumpWrapper()
    {
        if (!freezeInput) StartCoroutine(MobileJump());
    }
    IEnumerator MobileJump() { keyJump = true; yield return new WaitForSeconds(0.01f); keyJump = false; }

    public void SimulateMoveStop() => keyHorizontal = 0f;
    public void SimulateMoveLeft() => keyHorizontal = -1.0f;
    public void SimulateMoveRight() => keyHorizontal = 1.0f;
    public void SimulateShoot() => StartCoroutine(MobileShoot());
    public void SimulateJump() => StartCoroutine(MobileJump());
}
